using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.Addressables.Editor.Tests")]
#endif
[assembly: InternalsVisibleTo("Unity.Addressables.Tests")]
namespace UnityEngine.AddressableAssets
{
    internal class AddressablesImpl : IEqualityComparer<IResourceLocation>
    {
        ResourceManager m_ResourceManager;
        IInstanceProvider m_InstanceProvider;
        internal const string kCacheDataFolder = "{UnityEngine.Application.persistentDataPath}/com.unity.addressables/";

        public IInstanceProvider InstanceProvider
        {
            get
            {
                return m_InstanceProvider;
            }
            set
            {
                m_InstanceProvider = value;
                var rec = m_InstanceProvider as IUpdateReceiver;
                if (rec != null)
                    m_ResourceManager.AddUpdateReceiver(rec);
            }
        }
        public ISceneProvider SceneProvider;
        public ResourceManager ResourceManager
        {
            get
            {
                if (m_ResourceManager == null)
                    m_ResourceManager = new ResourceManager(new DefaultAllocationStrategy());
                return m_ResourceManager;
            }
        }

        public class ResourceLocatorInfo
        {
            public IResourceLocator Locator { get; private set; }
            public string LocalHash { get; private set; }
            public IResourceLocation CatalogLocation { get; private set; }
            public bool ContentUpdateAvailable { get; internal set; }
            public ResourceLocatorInfo(IResourceLocator loc, string localHash, IResourceLocation remoteCatalogLocation)
            {
                Locator = loc;
                LocalHash = localHash;
                CatalogLocation = remoteCatalogLocation;
            }

            public IResourceLocation HashLocation
            {
                get
                {
                    return CatalogLocation.Dependencies[0];
                }
            }

            public bool CanUpdateContent
            {
                get
                {
                    return !string.IsNullOrEmpty(LocalHash) && CatalogLocation != null && CatalogLocation.HasDependencies && CatalogLocation.Dependencies.Count == 2;
                }
            }

            internal void UpdateContent(IResourceLocator locator, string hash, IResourceLocation loc)
            {
                LocalHash = hash;
                CatalogLocation = loc;
                Locator = locator;
            }
        }

        internal List<ResourceLocatorInfo> m_ResourceLocators = new List<ResourceLocatorInfo>();
        AsyncOperationHandle<IResourceLocator> m_InitializationOperation;
        AsyncOperationHandle<List<string>> m_ActiveCheckUpdateOperation;
        AsyncOperationHandle<List<IResourceLocator>> m_ActiveUpdateOperation;


        Action<AsyncOperationHandle> m_OnHandleCompleteAction;
        Action<AsyncOperationHandle> m_OnSceneHandleCompleteAction;
        Action<AsyncOperationHandle> m_OnHandleDestroyedAction;
        Dictionary<object, AsyncOperationHandle> m_resultToHandle = new Dictionary<object, AsyncOperationHandle>();
        internal HashSet<AsyncOperationHandle> m_SceneInstances = new HashSet<AsyncOperationHandle>();

        internal int SceneOperationCount { get { return m_SceneInstances.Count; } }
        internal int TrackedHandleCount { get { return m_resultToHandle.Count; } }
        internal bool hasStartedInitialization = false;
        public AddressablesImpl(IAllocationStrategy alloc)
        {
            m_ResourceManager = new ResourceManager(alloc);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        internal void ReleaseSceneManagerOperation()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        public Func<IResourceLocation, string> InternalIdTransformFunc
        {
            get { return ResourceManager.InternalIdTransformFunc; }
            set { ResourceManager.InternalIdTransformFunc = value; }
        }


        public AsyncOperationHandle ChainOperation
        {
            get
            {
                if (!hasStartedInitialization)
                    return InitializeAsync();
                if (m_InitializationOperation.IsValid() && !m_InitializationOperation.IsDone)
                    return m_InitializationOperation;
                if (m_ActiveUpdateOperation.IsValid() && !m_ActiveUpdateOperation.IsDone)
                    return m_ActiveUpdateOperation;
                Debug.LogWarning($"{nameof(ChainOperation)} property should not be accessed unless {nameof(ShouldChainRequest)} is true.");
                return default;
            }
        }

        internal bool ShouldChainRequest
        {
            get
            {
                if (!hasStartedInitialization)
                    return true;

                if (m_InitializationOperation.IsValid() && !m_InitializationOperation.IsDone)
                    return true;

                return m_ActiveUpdateOperation.IsValid() && !m_ActiveUpdateOperation.IsDone;
            }
        }

        internal void OnSceneUnloaded(Scene scene)
        {
            foreach (var s in m_SceneInstances)
            {
                if (!s.IsValid())
                {
                    m_SceneInstances.Remove(s);
                    break;
                }

                var sceneHandle = s.Convert<SceneInstance>();
                if (sceneHandle.Result.Scene == scene)
                {
                    m_SceneInstances.Remove(s);
                    m_resultToHandle.Remove(s.Result);
                    var op = m_ResourceManager.ReleaseScene(SceneProvider, sceneHandle);
                    op.Completed += handle => { Release(op); };
                    break;
                }
            }
            m_ResourceManager.CleanupSceneInstances(scene);
        }

        public string StreamingAssetsSubFolder
        {
            get
            {
                return "aa";
            }
        }

        public string BuildPath
        {
            get { return "Library/com.unity.addressables/" + StreamingAssetsSubFolder + "/" + PlatformMappingService.GetPlatform(); }
        }

        public string PlayerBuildDataPath
        {
            get
            {
                return Application.streamingAssetsPath + "/" + StreamingAssetsSubFolder;
            }
        }

        public string RuntimePath
        {
            get
            {
#if UNITY_EDITOR
                return BuildPath;
#else
                return PlayerBuildDataPath;
#endif
            }
        }

        public void Log(string msg)
        {
            Debug.Log(msg);
        }

        public void LogFormat(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        public void LogWarning(string msg)
        {
            Debug.LogWarning(msg);
        }

        public void LogWarningFormat(string format, params object[] args)
        {
            Debug.LogWarningFormat(format, args);
        }

        public void LogError(string msg)
        {
            Debug.LogError(msg);
        }

        public void LogException(AsyncOperationHandle op, Exception ex)
        {
            if (op.Status == AsyncOperationStatus.Failed)
                Debug.LogErrorFormat("{0} encountered in operation {1}, status={2}, result={3} : {4}", ex.GetType().Name, op.DebugName, op.Status, op.Result, ex.Message);
            else
                Addressables.LogFormat("{0} encountered in operation {1}, status={2}, result={3} : {2}", ex.GetType().Name, op.DebugName, op.Status, op.Result, ex.Message);
        }

        public void LogErrorFormat(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        public string ResolveInternalId(string id)
        {
            var path = AddressablesRuntimeProperties.EvaluateString(id);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_XBOXONE
            if (path.Length >= 260 && path.StartsWith(Application.dataPath))
                path = path.Substring(Application.dataPath.Length + 1);
#endif
            return path;
        }

        public IEnumerable<IResourceLocator> ResourceLocators
        {
            get
            {
                return m_ResourceLocators.Select(l => l.Locator);
            }
        }

        public void AddResourceLocator(IResourceLocator loc, string localCatalogHash = null, IResourceLocation remoteCatalogLocation = null)
        {
            m_ResourceLocators.Add(new ResourceLocatorInfo(loc, localCatalogHash, remoteCatalogLocation));
        }

        public void RemoveResourceLocator(IResourceLocator loc)
        {
            m_ResourceLocators.RemoveAll(l => l.Locator == loc);
        }

        public void ClearResourceLocators()
        {
            m_ResourceLocators.Clear();
        }

        internal bool GetResourceLocations(object key, Type type, out IList<IResourceLocation> locations)
        {
            if (type == null && (key is AssetReference))
                type = (key as AssetReference).SubOjbectType;

            key = EvaluateKey(key);

            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var locatorInfo in m_ResourceLocators)
            {
                var locator = locatorInfo.Locator;
                IList<IResourceLocation> locs;
                if (locator.Locate(key, type, out locs))
                {
                    if (locations == null)
                    {
                        //simple, common case, no allocations
                        locations = locs;
                    }
                    else
                    {
                        //less common, need to merge...
                        if (current == null)
                        {
                            current = new HashSet<IResourceLocation>();
                            foreach (var loc in locations)
                                current.Add(loc);
                        }

                        current.UnionWith(locs);
                    }
                }
            }

            if (current == null)
                return locations != null;

            locations = new List<IResourceLocation>(current);
            return true;
        }

        internal bool GetResourceLocations(IEnumerable keys, Type type, Addressables.MergeMode merge, out IList<IResourceLocation> locations)
        {
            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var key in keys)
            {
                IList<IResourceLocation> locs;
                if (GetResourceLocations(key, type, out locs))
                {
                    if (locations == null)
                    {
                        locations = locs;
                        if (merge == Addressables.MergeMode.UseFirst)
                            return true;
                    }
                    else
                    {
                        if (current == null)
                        {
                            current = new HashSet<IResourceLocation>(locations, this);
                        }

                        if (merge == Addressables.MergeMode.Intersection)
                            current.IntersectWith(locs);
                        else if (merge == Addressables.MergeMode.Union)
                            current.UnionWith(locs);
                    }
                }
                else
                {
                    //if entries for a key are not found, the intersection is empty
                    if (merge == Addressables.MergeMode.Intersection)
                    {
                        locations = null;
                        return false;
                    }
                }
            }

            if (current == null)
                return locations != null;
            if (current.Count == 0)
            {
                locations = null;
                return false;
            }
            locations = new List<IResourceLocation>(current);
            return true;
        }

        public AsyncOperationHandle<IResourceLocator> InitializeAsync(string runtimeDataPath, string providerSuffix = null, bool autoReleaseHandle = true)
        {
            if (hasStartedInitialization)
            {
                if (m_InitializationOperation.IsValid())
                    return m_InitializationOperation;
                return ResourceManager.CreateCompletedOperation(m_ResourceLocators[0].Locator, null);
            }

            if (ResourceManager.ExceptionHandler == null)
            {
                ResourceManager.ExceptionHandler = LogException;
            }
            hasStartedInitialization = true;
            if (m_InitializationOperation.IsValid())
                return m_InitializationOperation;
            //these need to be referenced in order to prevent stripping on IL2CPP platforms.
            if (string.IsNullOrEmpty(Application.streamingAssetsPath))
                Addressables.LogWarning("Application.streamingAssetsPath has been stripped!");
#if !UNITY_SWITCH
            if (string.IsNullOrEmpty(Application.persistentDataPath))
                Addressables.LogWarning("Application.persistentDataPath has been stripped!");
#endif
            if (string.IsNullOrEmpty(runtimeDataPath))
                return ResourceManager.CreateCompletedOperation<IResourceLocator>(null, string.Format("Invalid Key: {0}", runtimeDataPath));

            m_OnHandleCompleteAction = OnHandleCompleted;
            m_OnSceneHandleCompleteAction = OnSceneHandleCompleted;
            m_OnHandleDestroyedAction = OnHandleDestroyed;

#if UNITY_EDITOR
            //this indicates that a specific addressables settings asset is being used for the runtime locations
            if (runtimeDataPath.StartsWith("GUID:"))
            {
                var assembly = Assembly.Load("Unity.Addressables.Editor");
                var settingsType = assembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings");
                var settingsGUID = runtimeDataPath.Substring(runtimeDataPath.IndexOf(':') + 1);
                var settingsPath = UnityEditor.AssetDatabase.GUIDToAssetPath(settingsGUID);
                var settingsObj = UnityEditor.AssetDatabase.LoadAssetAtPath(settingsPath, settingsType);
                var settingsSetupMethod = settingsType.GetMethod("CreatePlayModeInitializationOperation", BindingFlags.Instance | BindingFlags.NonPublic);
                m_InitializationOperation = (AsyncOperationHandle<IResourceLocator>)settingsSetupMethod.Invoke(settingsObj, new object[] { this });
            }
            else
#endif

            m_InitializationOperation = Initialization.InitializationOperation.CreateInitializationOperation(this, runtimeDataPath, providerSuffix);
            if (autoReleaseHandle)
                m_InitializationOperation.Completed += (x) => ResourceManager.Release(x);

            return m_InitializationOperation;
        }

        public AsyncOperationHandle<IResourceLocator> InitializeAsync()
        {
            var settingsPath =
#if UNITY_EDITOR
                PlayerPrefs.GetString(Addressables.kAddressablesRuntimeDataPath, RuntimePath + "/settings.json");
#else
                RuntimePath + "/settings.json";
#endif
            return InitializeAsync(ResolveInternalId(settingsPath));
        }

        internal ResourceLocationBase CreateCatalogLocationWithHashDependencies(string catalogPath, string hashFilePath)
        {
            var catalogLoc = new ResourceLocationBase(catalogPath, catalogPath, typeof(ContentCatalogProvider).FullName, typeof(IResourceLocator));

            if (!string.IsNullOrEmpty(hashFilePath))
            {
                string cacheHashFilePath = ResolveInternalId(kCacheDataFolder + Path.GetFileName(hashFilePath));
                
                var hashResourceLocation = new ResourceLocationBase(hashFilePath, hashFilePath, typeof(TextDataProvider).FullName, typeof(string));
                hashResourceLocation.Data = new ProviderLoadRequestOptions() {IgnoreFailures = true};
                catalogLoc.Dependencies.Add(hashResourceLocation);
                var cacheResourceLocation = new ResourceLocationBase(cacheHashFilePath, cacheHashFilePath, typeof(TextDataProvider).FullName, typeof(string));
                cacheResourceLocation.Data = new ProviderLoadRequestOptions() {IgnoreFailures = true};
                catalogLoc.Dependencies.Add(cacheResourceLocation);
            }

            return catalogLoc;
        }

        public AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath, bool autoReleaseHandle = true, string providerSuffix = null)
        {
            string catalogHashPath = catalogPath.Replace(".json", ".hash");
            var catalogLoc = CreateCatalogLocationWithHashDependencies(catalogPath, catalogHashPath);
            if (ShouldChainRequest)
                return ResourceManager.CreateChainOperation(ChainOperation, op => LoadContentCatalogAsync(catalogPath, autoReleaseHandle, providerSuffix));
            var handle = Initialization.InitializationOperation.LoadContentCatalog(this, catalogLoc, providerSuffix);
            if (autoReleaseHandle)
            {
                handle.Completed += (obj =>
                {
                    Release(handle);
                });
            }
            return handle;
        }

        AsyncOperationHandle<SceneInstance> TrackHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            handle.Completed += (sceneHandle) =>
            {
                m_OnSceneHandleCompleteAction(sceneHandle);
            };
            return handle;
        }

        AsyncOperationHandle<TObject> TrackHandle<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.CompletedTypeless += m_OnHandleCompleteAction;
            return handle;
        }

        AsyncOperationHandle TrackHandle(AsyncOperationHandle handle)
        {
            handle.Completed += m_OnHandleCompleteAction;
            return handle;
        }

        internal void ClearTrackHandles()
        {
            m_resultToHandle.Clear();
        }

        public AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(IResourceLocation location)
        {
            return TrackHandle(ResourceManager.ProvideResource<TObject>(location));
        }

        AsyncOperationHandle<TObject> LoadAssetWithChain<TObject>(AsyncOperationHandle dep, object key)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadAssetAsync<TObject>(key));
        }

        public AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(object key)
        {
            if (ShouldChainRequest)
                return TrackHandle(LoadAssetWithChain<TObject>(ChainOperation, key));

            key = EvaluateKey(key);

            IList<IResourceLocation> locs;
            var t = typeof(TObject);
            if (t.IsArray)
                t = t.GetElementType();
            else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                t = t.GetGenericArguments()[0];
            foreach (var locatorInfo in m_ResourceLocators)
            {
                var locator = locatorInfo.Locator;
                if (locator.Locate(key, t, out locs))
                {
                    foreach (var loc in locs)
                    {
                        var provider = ResourceManager.GetResourceProvider(typeof(TObject), loc);
                        if (provider != null)
                            return TrackHandle(ResourceManager.ProvideResource<TObject>(loc));
                    }
                }
            }
            return ResourceManager.CreateCompletedOperation<TObject>(default(TObject), new InvalidKeyException(key, t).Message);
        }

        class LoadResourceLocationKeyOp : AsyncOperationBase<IList<IResourceLocation>>
        {
            object m_Keys;
            IList<IResourceLocation> m_locations;
            AddressablesImpl m_Addressables;
            Type m_ResourceType;
            protected override string DebugName { get { return m_Keys.ToString(); } }

            public void Init(AddressablesImpl aa, Type t, object keys)
            {
                m_Keys = keys;
                m_ResourceType = t;
                m_Addressables = aa;
            }

            protected override void Execute()
            {
                m_Addressables.GetResourceLocations(m_Keys, m_ResourceType, out m_locations);
                if (m_locations == null)
                    m_locations = new List<IResourceLocation>();
                Complete(m_locations, true, string.Empty);
            }
        }

        class LoadResourceLocationKeysOp : AsyncOperationBase<IList<IResourceLocation>>
        {
            IEnumerable m_Key;
            Addressables.MergeMode m_MergeMode;
            IList<IResourceLocation> m_locations;
            AddressablesImpl m_Addressables;
            Type m_ResourceType;

            protected override string DebugName { get { return "LoadResourceLocationKeysOp"; } }
            public void Init(AddressablesImpl aa, Type t, IEnumerable key, Addressables.MergeMode mergeMode)
            {
                m_Key = key;
                m_ResourceType = t;
                m_MergeMode = mergeMode;
                m_Addressables = aa;
            }

            protected override void Execute()
            {
                m_Addressables.GetResourceLocations(m_Key, m_ResourceType, m_MergeMode, out m_locations);
                if (m_locations == null)
                    m_locations = new List<IResourceLocation>();
                Complete(m_locations, true, string.Empty);
            }
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsWithChain(AsyncOperationHandle dep, IEnumerable keys, Addressables.MergeMode mode, Type type)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadResourceLocationsAsync(keys, mode, type));
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(IEnumerable keys, Addressables.MergeMode mode, Type type = null)
        {
            if (ShouldChainRequest)
                return TrackHandle(LoadResourceLocationsWithChain(ChainOperation, keys, mode, type));

            var op = new LoadResourceLocationKeysOp();
            op.Init(this, type, keys, mode);
            return TrackHandle(ResourceManager.StartOperation(op, default));
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsWithChain(AsyncOperationHandle dep, object key, Type type)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadResourceLocationsAsync(key, type));
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(object key, Type type = null)
        {
            if (ShouldChainRequest)
                return TrackHandle(LoadResourceLocationsWithChain(ChainOperation, key, type));

            var op = new LoadResourceLocationKeyOp();
            op.Init(this, type, key);
            return TrackHandle(ResourceManager.StartOperation(op, default));
        }

        public AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> locations, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            return TrackHandle(ResourceManager.ProvideResources(locations, releaseDependenciesOnFailure, callback));
        }

        AsyncOperationHandle<IList<TObject>> LoadAssetsWithChain<TObject>(AsyncOperationHandle dep, IEnumerable keys, Action<TObject> callback, Addressables.MergeMode mode, bool releaseDependenciesOnFailure)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadAssetsAsync(keys, callback, mode, releaseDependenciesOnFailure));
        }

        public AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IEnumerable keys, Action<TObject> callback, Addressables.MergeMode mode, bool releaseDependenciesOnFailure)
        {
            if (ShouldChainRequest)
                return TrackHandle(LoadAssetsWithChain(ChainOperation, keys, callback, mode, releaseDependenciesOnFailure));

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(keys, typeof(TObject), mode, out locations))
                return ResourceManager.CreateCompletedOperation<IList<TObject>>(null, new InvalidKeyException(keys, typeof(TObject)).Message);

            return LoadAssetsAsync(locations, callback, releaseDependenciesOnFailure);
        }

        AsyncOperationHandle<IList<TObject>> LoadAssetsWithChain<TObject>(AsyncOperationHandle dep, object key, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            return ResourceManager.CreateChainOperation(dep, op2 => LoadAssetsAsync(key, callback, releaseDependenciesOnFailure));
        }

        public AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            if (ShouldChainRequest)
                return TrackHandle(LoadAssetsWithChain(ChainOperation, key, callback, releaseDependenciesOnFailure));

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, typeof(TObject), out locations))
                return ResourceManager.CreateCompletedOperation<IList<TObject>>(null, new InvalidKeyException(key, typeof(TObject)).Message);

            return LoadAssetsAsync(locations, callback, releaseDependenciesOnFailure);
        }

        void OnHandleDestroyed(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                m_resultToHandle.Remove(handle.Result);
            }
        }

        void OnSceneHandleCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                m_SceneInstances.Add(handle);
                if (!m_resultToHandle.ContainsKey(handle.Result))
                {
                    handle.Destroyed += m_OnHandleDestroyedAction;
                    m_resultToHandle.Add(handle.Result, handle);
                }
            }
        }

        void OnHandleCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                if (!m_resultToHandle.ContainsKey(handle.Result))
                {
                    handle.Destroyed += m_OnHandleDestroyedAction;
                    m_resultToHandle.Add(handle.Result, handle);
                }
            }
        }

        public void Release<TObject>(TObject obj)
        {
            if (obj == null)
            {
                LogWarning("Addressables.Release() - trying to release null object.");
                return;
            }

            AsyncOperationHandle handle;
            if (m_resultToHandle.TryGetValue(obj, out handle))
                Release(handle);
            else
            {
                LogError("Addressables.Release was called on an object that Addressables was not previously aware of.  Thus nothing is being released");
            }
        }

        public void Release<TObject>(AsyncOperationHandle<TObject> handle)
        {
            m_ResourceManager.Release(handle);
        }

        public void Release(AsyncOperationHandle handle)
        {
            m_ResourceManager.Release(handle);
        }

        AsyncOperationHandle<long> GetDownloadSizeWithChain(AsyncOperationHandle dep, object key)
        {
            return ResourceManager.CreateChainOperation(dep, op => GetDownloadSizeAsync(key));
        }

        AsyncOperationHandle<long> GetDownloadSizeWithChain(AsyncOperationHandle dep, IEnumerable keys)
        {
            return ResourceManager.CreateChainOperation(dep, op => GetDownloadSizeAsync(keys));
        }

        public AsyncOperationHandle<long> GetDownloadSizeAsync(object key)
        {
            return GetDownloadSizeAsync(new object[] { key });
        }

        public AsyncOperationHandle<long> GetDownloadSizeAsync(IEnumerable keys)
        {
            if (ShouldChainRequest)
                return TrackHandle(GetDownloadSizeWithChain(ChainOperation, keys));

            List<IResourceLocation> allLocations = new List<IResourceLocation>();
            foreach (object key in keys)
            {
                IList<IResourceLocation> locations;
                if (key is IList<IResourceLocation>)
                    locations = key as IList<IResourceLocation>;
                else if (key is IResourceLocation)
                {
                    locations = new List<IResourceLocation>(1)
                    {
                        key as IResourceLocation
                    };
                }
                else if (!GetResourceLocations(key, typeof(object), out locations))
                    return ResourceManager.CreateCompletedOperation<long>(0, new InvalidKeyException(key, typeof(object)).Message);

                foreach (var loc in locations)
                {
                    if (loc.HasDependencies)
                        allLocations.AddRange(loc.Dependencies);
                }
            }

            long size = 0;
            foreach (IResourceLocation location in allLocations.Distinct())
            {
                var sizeData = location.Data as ILocationSizeData;
                if (sizeData != null)
                    size += sizeData.ComputeSize(location, ResourceManager);
            }

            return ResourceManager.CreateCompletedOperation<long>(size, string.Empty);
        }

        AsyncOperationHandle DownloadDependenciesAsyncWithChain(AsyncOperationHandle dep, object key, bool autoReleaseHandle)
        {
            var handle = ResourceManager.CreateChainOperation(dep, op => DownloadDependenciesAsync(key).Convert<IList<IAssetBundleResource>>());
            if (autoReleaseHandle)
                handle.Completed += op => Release(op);
            return handle;
        }

        static List<IResourceLocation> GatherDependenciesFromLocations(IList<IResourceLocation> locations)
        {
            var locHash = new HashSet<IResourceLocation>();
            foreach (var loc in locations)
            {
                if (loc.ResourceType == typeof(IAssetBundleResource))
                {
                    locHash.Add(loc);
                }
                if (loc.HasDependencies)
                {
                    foreach (var dep in loc.Dependencies)
                        if (dep.ResourceType == typeof(IAssetBundleResource))
                            locHash.Add(dep);
                }
            }
            return new List<IResourceLocation>(locHash);
        }

        public AsyncOperationHandle DownloadDependenciesAsync(object key, bool autoReleaseHandle = false)
        {
            if (ShouldChainRequest)
                return DownloadDependenciesAsyncWithChain(ChainOperation, key, autoReleaseHandle);

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, typeof(object), out locations))
            {
                var handle = ResourceManager.CreateCompletedOperation<IList<IAssetBundleResource>>(null,
                    new InvalidKeyException(key, typeof(object)).Message);
                if (autoReleaseHandle)
                    handle.Completed += op => Release(op);
                return handle;
            }
            else
            {
                var handle = LoadAssetsAsync<IAssetBundleResource>(GatherDependenciesFromLocations(locations), null, true);
                if (autoReleaseHandle)
                    handle.Completed += op => Release(op);
                return handle;
            }
        }

        AsyncOperationHandle DownloadDependenciesAsyncWithChain(AsyncOperationHandle dep, IList<IResourceLocation> locations, bool autoReleaseHandle)
        {
            var handle = ResourceManager.CreateChainOperation(dep, op => DownloadDependenciesAsync(locations).Convert<IList<IAssetBundleResource>>());
            if (autoReleaseHandle)
                handle.Completed += op => Release(op);
            return handle;
        }

        public AsyncOperationHandle DownloadDependenciesAsync(IList<IResourceLocation> locations, bool autoReleaseHandle = false)
        {
            if (ShouldChainRequest)
                return DownloadDependenciesAsyncWithChain(ChainOperation, locations, autoReleaseHandle);

            var handle = LoadAssetsAsync<IAssetBundleResource>(GatherDependenciesFromLocations(locations), null, true);
            if (autoReleaseHandle)
                handle.Completed += op => Release(op);
            return handle;
        }

        AsyncOperationHandle DownloadDependenciesAsyncWithChain(AsyncOperationHandle dep, IEnumerable keys, Addressables.MergeMode mode, bool autoReleaseHandle)
        {
            var handle = ResourceManager.CreateChainOperation(dep, op => DownloadDependenciesAsync(keys, mode).Convert<IList<IAssetBundleResource>>());
            if (autoReleaseHandle)
                handle.Completed += op => Release(op);
            return handle;
        }

        public AsyncOperationHandle DownloadDependenciesAsync(IEnumerable keys, Addressables.MergeMode mode, bool autoReleaseHandle = false)
        {
            if (ShouldChainRequest)
                return DownloadDependenciesAsyncWithChain(ChainOperation, keys, mode, autoReleaseHandle);

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(keys, typeof(object), mode, out locations))
            {
                var handle = ResourceManager.CreateCompletedOperation<IList<IAssetBundleResource>>(null,
                    new InvalidKeyException(keys, typeof(object)).Message);
                if (autoReleaseHandle)
                    handle.Completed += op => Release(op);
                return handle;
            }
            else
            {
                var handle = LoadAssetsAsync<IAssetBundleResource>(GatherDependenciesFromLocations(locations), null, true);
                if (autoReleaseHandle)
                    handle.Completed += op => Release(op);
                return handle;
            }
        }

        internal bool ClearDependencyCacheForKey(object key)
        {
            bool result = true;
#if ENABLE_CACHING
            IList<IResourceLocation> locations;
            if (key is IResourceLocation && (key as IResourceLocation).HasDependencies)
            {
                foreach (var dep in GatherDependenciesFromLocations((key as IResourceLocation).Dependencies))
                {
                    //This should never be false when we get here, if it is there's likely a deeper problem.
                    if (dep.Data is AssetBundleRequestOptions)
                        result = result && Caching.ClearAllCachedVersions((dep.Data as AssetBundleRequestOptions).BundleName);
                }
            }
            else if (GetResourceLocations(key, typeof(object), out locations))
            {
                foreach (var dep in GatherDependenciesFromLocations(locations))
                {
                    //This should never be false when we get here, if it is there's likely a deeper problem.
                    if (dep.Data is AssetBundleRequestOptions)
                        result = result && Caching.ClearAllCachedVersions((dep.Data as AssetBundleRequestOptions).BundleName);
                }
            }
#endif
            return result;
        }

        public AsyncOperationHandle<bool> ClearDependencyCacheAsync(object key, bool autoReleaseHandle)
        {
            if (ShouldChainRequest)
                return ResourceManager.CreateChainOperation(ChainOperation, op => ClearDependencyCacheAsync(key, autoReleaseHandle));

            bool result = ClearDependencyCacheForKey(key);

            var completedOp = ResourceManager.CreateCompletedOperation(result, result ? String.Empty : "Unable to clear the cache.  AssetBundle's may still be loaded for the given key.");
            if (autoReleaseHandle)
                Release(completedOp);
            return completedOp;
        }

        public AsyncOperationHandle<bool> ClearDependencyCacheAsync(IList<IResourceLocation> locations, bool autoReleaseHandle)
        {
            if (ShouldChainRequest)
                return ResourceManager.CreateChainOperation(ChainOperation, op => ClearDependencyCacheAsync(locations, autoReleaseHandle));

            bool result = true;
            foreach (var location in locations)
                result = result && ClearDependencyCacheForKey(location);

            var completedOp = ResourceManager.CreateCompletedOperation(result, result ? String.Empty : "Unable to clear the cache.  AssetBundle's may still be loaded for the given key(s).");
            if (autoReleaseHandle)
                Release(completedOp);
            return completedOp;
        }

        public AsyncOperationHandle<bool> ClearDependencyCacheAsync(IEnumerable keys, bool autoReleaseHandle)
        {
            if (ShouldChainRequest)
                return ResourceManager.CreateChainOperation(ChainOperation, op => ClearDependencyCacheAsync(keys, autoReleaseHandle));

            bool result = true;
            foreach (var key in keys)
                result = result && ClearDependencyCacheForKey(key);

            var completedOp = ResourceManager.CreateCompletedOperation(result, result ? String.Empty : "Unable to clear the cache.  AssetBundle's may still be loaded for the given key(s).");
            if (autoReleaseHandle)
                Release(completedOp);
            return completedOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(position, rotation, parent), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(key, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return InstantiateAsync(key, new InstantiationParameters(position, rotation, parent), trackHandle);
        }

        AsyncOperationHandle<GameObject> InstantiateWithChain(AsyncOperationHandle dep, object key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            var chainOp = ResourceManager.CreateChainOperation(dep, op => InstantiateAsync(key, instantiateParameters, false));
            if (trackHandle)
                chainOp.CompletedTypeless += m_OnHandleCompleteAction;
            return chainOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(object key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            if (ShouldChainRequest)
                return InstantiateWithChain(ChainOperation, key, instantiateParameters, trackHandle);

            key = EvaluateKey(key);
            IList<IResourceLocation> locs;
            foreach (var locatorInfo in m_ResourceLocators)
            {
                var locator = locatorInfo.Locator;
                if (locator.Locate(key, typeof(GameObject), out locs))
                    return InstantiateAsync(locs[0], instantiateParameters, trackHandle);
            }
            return ResourceManager.CreateCompletedOperation<GameObject>(null, new InvalidKeyException(key, typeof(GameObject)).Message);
        }

        AsyncOperationHandle<GameObject> InstantiateWithChain(AsyncOperationHandle dep, IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            var chainOp = ResourceManager.CreateChainOperation(dep, op => InstantiateAsync(location, instantiateParameters, false));
            if (trackHandle)
                chainOp.CompletedTypeless += m_OnHandleCompleteAction;
            return chainOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            if (ShouldChainRequest)
                return InstantiateWithChain(ChainOperation, location, instantiateParameters, trackHandle);

            var opHandle = ResourceManager.ProvideInstance(InstanceProvider, location, instantiateParameters);
            if (!trackHandle)
                return opHandle;
            opHandle.CompletedTypeless += m_OnHandleCompleteAction;
            return opHandle;
        }

        public bool ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                LogWarning("Addressables.ReleaseInstance() - trying to release null object.");
                return false;
            }

            AsyncOperationHandle handle;
            if (m_resultToHandle.TryGetValue(instance, out handle))
                Release(handle);
            else
                return false;

            return true;
        }

        internal AsyncOperationHandle<SceneInstance> LoadSceneWithChain(AsyncOperationHandle dep, object key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return TrackHandle(ResourceManager.CreateChainOperation(dep, op => LoadSceneAsync(key, loadMode, activateOnLoad, priority, false)));
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100, bool trackHandle = true)
        {
            if (ShouldChainRequest)
                return LoadSceneWithChain(ChainOperation, key, loadMode, activateOnLoad, priority);

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, typeof(SceneInstance), out locations))
                return ResourceManager.CreateCompletedOperation<SceneInstance>(default(SceneInstance), new InvalidKeyException(key, typeof(SceneInstance)).Message);

            return LoadSceneAsync(locations[0], loadMode, activateOnLoad, priority, trackHandle);
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100, bool trackHandle = true)
        {
            var handle = ResourceManager.ProvideScene(SceneProvider, location, loadMode, activateOnLoad, priority);
            if (trackHandle)
                return TrackHandle(handle);

            return handle;
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle;
            if (!m_resultToHandle.TryGetValue(scene, out handle))
            {
                var msg = string.Format("Addressables.UnloadSceneAsync() - Cannot find handle for scene {0}", scene);
                LogWarning(msg);
                return ResourceManager.CreateCompletedOperation<SceneInstance>(scene, msg);
            }

            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, autoReleaseHandle);

            return UnloadSceneAsync(handle, autoReleaseHandle);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, bool autoReleaseHandle = true)
        {
            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, autoReleaseHandle);

            return UnloadSceneAsync(handle.Convert<SceneInstance>(), autoReleaseHandle);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle = true)
        {
            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, autoReleaseHandle);

            return InternalUnloadScene(handle, autoReleaseHandle);
        }

        internal AsyncOperationHandle<SceneInstance> CreateUnloadSceneWithChain(AsyncOperationHandle handle, bool autoReleaseHandle)
        {
            return m_ResourceManager.CreateChainOperation(handle, (completedHandle) => InternalUnloadScene(completedHandle.Convert<SceneInstance>(), autoReleaseHandle));
        }

        internal AsyncOperationHandle<SceneInstance> CreateUnloadSceneWithChain(AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle)
        {
            return m_ResourceManager.CreateChainOperation(handle, (completedHandle) => InternalUnloadScene(completedHandle, autoReleaseHandle));
        }

        internal AsyncOperationHandle<SceneInstance> InternalUnloadScene(AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle)
        {
            var relOp = ResourceManager.ReleaseScene(SceneProvider, handle);
            if (autoReleaseHandle)
                relOp.Completed += op => Release(op);
            return relOp;
        }

        private object EvaluateKey(object obj)
        {
            if (obj is IKeyEvaluator)
                return (obj as IKeyEvaluator).RuntimeKey;
            return obj;
        }

        internal AsyncOperationHandle<List<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true)
        {
            if (m_ActiveCheckUpdateOperation.IsValid())
                Release(m_ActiveCheckUpdateOperation);
            m_ActiveCheckUpdateOperation = new CheckCatalogsOperation(this).Start(m_ResourceLocators);
            if (autoReleaseHandle)
                m_ActiveCheckUpdateOperation.CompletedTypeless += o => ResourceManager.Release(o);
            return m_ActiveCheckUpdateOperation;
        }

        internal ResourceLocatorInfo GetLocatorInfo(string c)
        {
            foreach (var l in m_ResourceLocators)
                if (l.Locator.LocatorId == c)
                    return l;
            return null;
        }

        internal IEnumerable<string> CatalogsWithAvailableUpdates => m_ResourceLocators.Where(s => s.ContentUpdateAvailable).Select(s => s.Locator.LocatorId);
        internal AsyncOperationHandle<List<IResourceLocator>> UpdateCatalogs(IEnumerable<string> catalogIds = null, bool autoReleaseHandle = true)
        {
            if (m_ActiveUpdateOperation.IsValid())
                return m_ActiveUpdateOperation;
            if (catalogIds == null && !CatalogsWithAvailableUpdates.Any())
                return m_ResourceManager.CreateChainOperation(CheckForCatalogUpdates(), depOp => UpdateCatalogs(CatalogsWithAvailableUpdates, autoReleaseHandle));

            var op = new UpdateCatalogsOperation(this).Start(catalogIds == null ? CatalogsWithAvailableUpdates : catalogIds);
            if (autoReleaseHandle)
                op.CompletedTypeless += o => ResourceManager.Release(o);
            return op;
        }

        //needed for IEqualityComparer<IResourceLocation> interface
        public bool Equals(IResourceLocation x, IResourceLocation y)
        {
            return x.PrimaryKey.Equals(y.PrimaryKey) && x.ResourceType.Equals(y.ResourceType) && x.InternalId.Equals(y.InternalId);
        }

        //needed for IEqualityComparer<IResourceLocation> interface
        public int GetHashCode(IResourceLocation loc)
        {
            return loc.PrimaryKey.GetHashCode() * 31 + loc.ResourceType.GetHashCode();
        }
    }
}
