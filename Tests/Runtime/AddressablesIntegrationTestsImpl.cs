using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.AddressableAssets.Utility;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace AddressableAssetsIntegrationTests
{
    internal abstract partial class AddressablesIntegrationTests : IPrebuildSetup
    {
        [UnityTest]
        public IEnumerator AsyncCache_IsCleaned_OnFailedOperation()
        {
            yield return Init();

            AsyncOperationHandle<GameObject> op;
            using (new IgnoreFailingLogMessage())
                op = m_Addressables.LoadAssetAsync<GameObject>("notARealKey");

            op.Completed += handle =>
            {
                Assert.AreEqual(0, m_Addressables.ResourceManager.CachedOperationCount());
            };

            yield return op;
        }

        [UnityTest]
        public IEnumerator LoadResourceLocations_InvalidKeyDoesNotThrow()
        {
            //Setup
            yield return Init();

            //Test
            Assert.DoesNotThrow(() =>
            {
                m_Addressables.LoadResourceLocationsAsync("noSuchLabel", typeof(object));
            });
        }

        [UnityTest]
        public IEnumerator LoadResourceLocations_ValidKeyDoesNotThrow()
        {
            //Setup
            yield return Init();

            //Test
            Assert.DoesNotThrow(() =>
            {
                m_Addressables.LoadResourceLocationsAsync(AddressablesTestUtility.GetPrefabLabel("BASE"), typeof(GameObject));
            });
        }

        [UnityTest]
        public IEnumerator LoadPrefabWithComponentType_Fails()
        {
            //Setup
            yield return Init();

            //Test
            AsyncOperationHandle<MeshRenderer> op = new AsyncOperationHandle<MeshRenderer>();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.LoadAssetAsync<MeshRenderer>("test0BASE");
            }
            yield return op;
            Assert.IsNull(op.Result);
        }

        [UnityTest]
        public IEnumerator LoadAsset_InvalidKeyThrowsInvalidKeyException()
        {
            //Setup
            yield return Init();

            //Test
            AsyncOperationHandle handle = default(AsyncOperationHandle);
            using (new IgnoreFailingLogMessage())
            {
                handle = m_Addressables.LoadAssetAsync<GameObject>("noSuchLabel");
            }

            Assert.AreEqual("Exception of type 'UnityEngine.AddressableAssets.InvalidKeyException' was thrown., Key=noSuchLabel, Type=UnityEngine.GameObject", handle.OperationException.Message);
            yield return handle;

            //Cleanup
            handle.Release();
        }

        [UnityTest]
        public IEnumerator CanLoadTextureAsSprite()
        {
            //Setup
            yield return Init();

            var op = m_Addressables.LoadAssetAsync<Sprite>("sprite");
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(typeof(Sprite), op.Result.GetType());
            op.Release();
        }

        [UnityTest]
        public IEnumerator CanLoadSpriteByName()
        {
            //Setup
            yield return Init();

            var op = m_Addressables.LoadAssetAsync<Sprite>("sprite[botright]");
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(typeof(Sprite), op.Result.GetType());
            Assert.AreEqual("botright", op.Result.name);
            op.Release();

            var op2 = m_Addressables.LoadAssetAsync<Sprite>("sprite[topleft]");
            yield return op2;
            Assert.IsNotNull(op2.Result);
            Assert.AreEqual(typeof(Sprite), op2.Result.GetType());
            Assert.AreEqual("topleft", op2.Result.name);
            op2.Release();
        }

        [UnityTest]
        public IEnumerator CanLoadAllSpritesAsArray()
        {
            //Setup
            yield return Init();

            var op = m_Addressables.LoadAssetAsync<Sprite[]>("sprite");
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(typeof(Sprite[]), op.Result.GetType());
            Assert.AreEqual(2, op.Result.Length);
            op.Release();
        }

        [UnityTest]
        public IEnumerator CanLoadAllSpritesAsList()
        {
            //Setup
            yield return Init();

            var op = m_Addressables.LoadAssetAsync<IList<Sprite>>("sprite");
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.IsTrue(typeof(IList<Sprite>).IsAssignableFrom(op.Result.GetType()));
            Assert.AreEqual(2, op.Result.Count);
            op.Release();
        }

        string TransFunc(IResourceLocation loc)
        {
            return "transformed";
        }

        [UnityTest]
        public IEnumerator InternalIdTranslationTest()
        {
            //Setup
            yield return Init();
            m_Addressables.InternalIdTransformFunc = TransFunc;
            var loc = new ResourceLocationBase("none", "original", "none", typeof(object));
            var transformedId = m_Addressables.ResourceManager.TransformInternalId(loc);
            Assert.AreEqual("transformed", transformedId);
            m_Addressables.InternalIdTransformFunc = null;
            var originalId = m_Addressables.ResourceManager.TransformInternalId(loc);
            Assert.AreEqual("original", originalId);
        }

        [UnityTest]
        public IEnumerator CanLoadTextureAsTexture()
        {
            //Setup
            yield return Init();

            var op = m_Addressables.LoadAssetAsync<Texture2D>("sprite");
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(typeof(Texture2D), op.Result.GetType());
            op.Release();
        }

        [UnityTest]
        public IEnumerator LoadAsset_ValidKeyDoesNotThrow()
        {
            //Setup
            yield return Init();

            //Test
            AsyncOperationHandle handle = default(AsyncOperationHandle);
            Assert.DoesNotThrow(() =>
            {
                handle = m_Addressables.LoadAssetAsync<GameObject>(AddressablesTestUtility.GetPrefabLabel("BASE"));
            });
            yield return handle;

            //Cleanup
            handle.Release();
        }

        [UnityTest]
        public IEnumerator VerifyChainOpPercentCompleteCalculation()
        {
            //Setup
            yield return Init();
            AsyncOperationHandle<GameObject> op = m_Addressables.LoadAssetAsync<GameObject>(AddressablesTestUtility.GetPrefabLabel("BASE"));

            //Test
            while (op.PercentComplete < 1)
            {
                Assert.False(op.IsDone);
                yield return null;
            }

            yield return null;
            Assert.True(op.PercentComplete == 1 && op.IsDone);
            yield return op;

            //Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator LoadResourceLocationsAsync_ReturnsCorrectNumberOfLocationsForStringKey()
        {
            yield return Init();

            var handle = m_Addressables.LoadResourceLocationsAsync("assetWithDifferentTypedSubAssets");
            yield return handle;

            Assert.AreEqual(3, handle.Result.Count);
            HashSet<Type> typesSeen = new HashSet<Type>();
            foreach (var result in handle.Result)
            {
                Assert.IsNotNull(result.ResourceType);
                typesSeen.Add(result.ResourceType);
            }
            Assert.AreEqual(3, typesSeen.Count);
            m_Addressables.Release(handle);
        }

        [UnityTest]
        public IEnumerator LoadResourceLocationsAsync_ReturnsCorrectNumberOfLocationsForSubStringKey()
        {
            yield return Init();

            var handle = m_Addressables.LoadResourceLocationsAsync("assetWithDifferentTypedSubAssets[Mesh]");
            yield return handle;

            Assert.AreEqual(3, handle.Result.Count);
            HashSet<Type> typesSeen = new HashSet<Type>();
            foreach (var result in handle.Result)
            {
                Assert.IsNotNull(result.ResourceType);
                typesSeen.Add(result.ResourceType);
            }
            Assert.AreEqual(3, typesSeen.Count);
            m_Addressables.Release(handle);
        }

        [UnityTest]
        public IEnumerator LoadResourceLocationsAsync_ReturnsCorrectNumberOfLocationsForSubStringKey_WhenTypeIsPassedIn()
        {
            yield return Init();

            var handle = m_Addressables.LoadResourceLocationsAsync("assetWithDifferentTypedSubAssets[Mesh]", typeof(Mesh));
            yield return handle;

            Assert.AreEqual(1, handle.Result.Count);
            Assert.AreEqual(typeof(Mesh), handle.Result[0].ResourceType);

            m_Addressables.Release(handle);
        }

        [UnityTest]
        public IEnumerator LoadResourceLocationsAsync_ReturnsCorrectNumberOfLocationsForAssetReference()
        {
            yield return Init();

            AsyncOperationHandle assetReferenceHandle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return assetReferenceHandle;
            Assert.IsNotNull(assetReferenceHandle.Result as GameObject);
            AssetReferenceTestBehavior behavior =
                (assetReferenceHandle.Result as GameObject).GetComponent<AssetReferenceTestBehavior>();

            var handle = m_Addressables.LoadResourceLocationsAsync(behavior.ReferenceWithMultiTypedSubObject);
            yield return handle;

            Assert.AreEqual(3, handle.Result.Count);
            HashSet<Type> typesSeen = new HashSet<Type>();
            foreach (var result in handle.Result)
            {
                Assert.IsNotNull(result.ResourceType);
                typesSeen.Add(result.ResourceType);
            }
            Assert.AreEqual(3, typesSeen.Count);

            m_Addressables.Release(assetReferenceHandle);
            m_Addressables.Release(handle);
        }

        [UnityTest]
        public IEnumerator LoadResourceLocationsAsync_ReturnsCorrectNumberOfLocationsForSubAssetReference()
        {
            yield return Init();

            AsyncOperationHandle assetReferenceHandle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return assetReferenceHandle;
            Assert.IsNotNull(assetReferenceHandle.Result as GameObject);
            AssetReferenceTestBehavior behavior =
                (assetReferenceHandle.Result as GameObject).GetComponent<AssetReferenceTestBehavior>();

            var handle = m_Addressables.LoadResourceLocationsAsync(behavior.ReferenceWithMultiTypedSubObjectSubReference);
            yield return handle;

            Assert.AreEqual(1, handle.Result.Count);
            Assert.AreEqual(typeof(Material), handle.Result[0].ResourceType);

            m_Addressables.Release(assetReferenceHandle);
            m_Addressables.Release(handle);
        }

        [UnityTest]
        public IEnumerator PercentComplete_NeverHasDecreasedValue_WhenLoadingAsset()
        {
            //Setup
            yield return Init();
            AsyncOperationHandle<GameObject> op = m_Addressables.LoadAssetAsync<GameObject>(AddressablesTestUtility.GetPrefabLabel("BASE"));

            //Test
            float lastPercentComplete = 0f;
            while (!op.IsDone)
            {
                Assert.IsFalse(lastPercentComplete > op.PercentComplete);
                lastPercentComplete = op.PercentComplete;
                yield return null;
            }
            Assert.True(op.PercentComplete == 1 && op.IsDone);
            yield return op;

            //Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator LoadContentCatalogAsync_SetsUpLocalAndRemoteLocations()
        {
            yield return Init();
            string catalogPath = "fakeCatalogPath.json";
            string catalogHashPath = "fakeCatalogPath.hash";

            var loc = m_Addressables.CreateCatalogLocationWithHashDependencies(catalogPath, catalogHashPath);
            var remoteLocation = loc.Dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote];
            var cacheLocation = loc.Dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache];

            Assert.AreEqual(2, loc.Dependencies.Count);
            Assert.AreEqual(catalogHashPath, remoteLocation.ToString());
            Assert.AreEqual(m_Addressables.ResolveInternalId(AddressablesImpl.kCacheDataFolder + catalogHashPath), cacheLocation.ToString());
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalogTwice_DoesNotThrowException_WhenHandleIsntReleased()
        {
            yield return Init();
            
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            Directory.CreateDirectory(kCatalogFolderPath);
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
#if UNITY_EDITOR
                ContentCatalogData data = new ContentCatalogData(new List<ContentCatalogDataEntry>{
                    new ContentCatalogDataEntry(typeof(string), "testString", "test.provider", new []{"key"})
                }, "test_catalog");
                File.WriteAllText(fullRemotePath, JsonUtility.ToJson(data));
#else
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalogTwice_DoesNotThrowException_WhenHandleIsntReleased)} due to missing CatalogLocation.");
                yield break;
#endif
            }
            else
            {
                string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
                if (baseCatalogPath.StartsWith("file://"))
                    baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
                File.Copy(baseCatalogPath, fullRemotePath);
            }
            
            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;

            var op2 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op2;

            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op2.Status);

            m_Addressables.Release(op1);
            m_Addressables.Release(op2);
            if (Directory.Exists(kCatalogFolderPath))
                Directory.Delete(kCatalogFolderPath, true);
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalogWithCacheTwice_DoesNotThrowException_WhenHandleIsntReleased()
        {
            yield return Init();
            
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            Directory.CreateDirectory(kCatalogFolderPath);
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
#if UNITY_EDITOR
                ContentCatalogData data = new ContentCatalogData(new List<ContentCatalogDataEntry>{
                    new ContentCatalogDataEntry(typeof(string), "testString", "test.provider", new []{"key"})
                }, "test_catalog");
                File.WriteAllText(fullRemotePath, JsonUtility.ToJson(data));
#else
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalogWithCacheTwice_DoesNotThrowException_WhenHandleIsntReleased)} due to missing CatalogLocation.");
                yield break;
#endif
            }
            else
            {
                string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
                if (baseCatalogPath.StartsWith("file://"))
                    baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
                File.Copy(baseCatalogPath, fullRemotePath);
            }
            WriteHashFileForCatalog(fullRemotePath, "123");
            
            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;

            var op2 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op2;

            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op2.Status);

            m_Addressables.Release(op1);
            m_Addressables.Release(op2);
            if (Directory.Exists(kCatalogFolderPath))
                Directory.Delete(kCatalogFolderPath, true);
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalog_WithInvalidCatalogPath_Fails()
        {
            yield return Init();

            bool ignoreValue = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var op1 = m_Addressables.LoadContentCatalogAsync("notarealpath.json", false);
            yield return op1;

            Assert.AreEqual(AsyncOperationStatus.Failed, op1.Status);

            m_Addressables.Release(op1);
            LogAssert.ignoreFailingMessages = ignoreValue;
        }

        private const string kCatalogRemotePath = "remotecatalog.json";
        private const string kCatalogFolderPath = "Assets/CatalogTestFolder";

        private string WriteHashFileForCatalog(string catalogPath, string hash)
        {
            string hashPath = catalogPath.Replace(".json", ".hash");
            Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
            File.WriteAllText(hashPath, hash);
            return hashPath;
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalog_CachesCatalogData_IfValidHashFound()
        {
            yield return Init();
            
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            Directory.CreateDirectory(kCatalogFolderPath);
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
#if UNITY_EDITOR
                ContentCatalogData data = new ContentCatalogData(new List<ContentCatalogDataEntry>{
                    new ContentCatalogDataEntry(typeof(string), "testString", "test.provider", new []{"key"})
                }, "test_catalog");
                File.WriteAllText(fullRemotePath, JsonUtility.ToJson(data));
#else
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalog_CachesCatalogData_IfValidHashFound)} due to missing CatalogLocation.");
                yield break;
#endif
            }
            else
            {
                string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
                if (baseCatalogPath.StartsWith("file://"))
                    baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
                File.Copy(baseCatalogPath, fullRemotePath);
            }
            WriteHashFileForCatalog(fullRemotePath, "123");

            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;

            string cachedDataPath = m_Addressables.ResolveInternalId(AddressablesImpl.kCacheDataFolder + Path.GetFileName(kCatalogRemotePath));
            string cachedHashPath = cachedDataPath.Replace(".json", ".hash");
            Assert.IsTrue(File.Exists(cachedDataPath));
            Assert.IsTrue(File.Exists(cachedHashPath));
            Assert.AreEqual("123", File.ReadAllText(cachedHashPath));

            m_Addressables.Release(op1);
            Directory.Delete(kCatalogFolderPath, true);
            File.Delete(cachedDataPath);
            File.Delete(cachedHashPath);
        }
        
        [UnityTest]
        public IEnumerator LoadingContentCatalog_IfNoCachedHashFound_Succeeds()
        {
            yield return Init();
            
            ResourceManager.ExceptionHandler = m_PrevHandler;
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            Directory.CreateDirectory(kCatalogFolderPath);
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
#if UNITY_EDITOR
                ContentCatalogData data = new ContentCatalogData(new List<ContentCatalogDataEntry>{
                    new ContentCatalogDataEntry(typeof(string), "testString", "test.provider", new []{"key"})
                }, "test_catalog");
                File.WriteAllText(fullRemotePath, JsonUtility.ToJson(data));
#else
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalog_IfNoCachedHashFound_Succeeds)} due to missing CatalogLocation.");
                yield break;
#endif
            }
            else
            {
                string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
                if (baseCatalogPath.StartsWith("file://"))
                    baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
                File.Copy(baseCatalogPath, fullRemotePath);
            }
            WriteHashFileForCatalog(fullRemotePath, "123");
            
            string cachedDataPath = m_Addressables.ResolveInternalId(AddressablesImpl.kCacheDataFolder + Path.GetFileName(kCatalogRemotePath));
            string cachedHashPath = cachedDataPath.Replace(".json", ".hash");
            if (File.Exists(cachedDataPath))
                File.Delete(cachedDataPath);
            if (File.Exists(cachedHashPath))
                File.Delete(cachedHashPath);
            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;
            
            Assert.IsTrue(op1.IsValid());
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.NotNull(op1.Result);
            
            // Cleanup
            Addressables.Release(op1);
            Directory.Delete(kCatalogFolderPath, true);
            File.Delete(cachedDataPath);
            File.Delete(cachedHashPath);
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalog_IfNoHashFileForCatalog_DoesntThrowException()
        {
            yield return Init();
            
            ResourceManager.ExceptionHandler = m_PrevHandler;
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            Directory.CreateDirectory(kCatalogFolderPath);
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
#if UNITY_EDITOR
                ContentCatalogData data = new ContentCatalogData(new List<ContentCatalogDataEntry>{
                    new ContentCatalogDataEntry(typeof(string), "testString", "test.provider", new []{"key"})
                }, "test_catalog");
                File.WriteAllText(fullRemotePath, JsonUtility.ToJson(data));
#else
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalog_IfNoCachedHashFound_Succeeds)} due to missing CatalogLocation.");
                yield break;
#endif
            }
            else
            {
                string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
                if (baseCatalogPath.StartsWith("file://"))
                    baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
                File.Copy(baseCatalogPath, fullRemotePath);
            }

            string cachedDataPath = m_Addressables.ResolveInternalId(AddressablesImpl.kCacheDataFolder + Path.GetFileName(kCatalogRemotePath));
            string cachedHashPath = cachedDataPath.Replace(".json", ".hash");
            if (File.Exists(cachedDataPath))
                File.Delete(cachedDataPath);
            if (File.Exists(cachedHashPath))
                File.Delete(cachedHashPath);
            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;
            
            Assert.IsTrue(op1.IsValid());
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.NotNull(op1.Result);
            Assert.IsFalse(File.Exists(cachedHashPath));
            
            // Cleanup
            Addressables.Release(op1);
            Directory.Delete(kCatalogFolderPath, true);
            File.Delete(cachedDataPath);
            File.Delete(cachedHashPath);
        }
        
        [UnityTest]
        public IEnumerator IResourceLocationComparing_SameKeySameTypeDifferentInternalId_ReturnsFalse()
        {
            yield return Init();

            IResourceLocation loc1 = new ResourceLocationBase("address", "internalid1", typeof(BundledAssetProvider).FullName, typeof(GameObject));
            IResourceLocation loc2 = new ResourceLocationBase("address", "internalid2", typeof(BundledAssetProvider).FullName, typeof(GameObject));

            Assert.IsFalse(m_Addressables.Equals(loc1, loc2));
        }

        [UnityTest]
        public IEnumerator IResourceLocationComparing_SameKeyTypeAndInternalId_ReturnsTrue()
        {
            yield return Init();

            IResourceLocation loc1 = new ResourceLocationBase("address", "internalid1", typeof(BundledAssetProvider).FullName, typeof(GameObject));
            IResourceLocation loc2 = new ResourceLocationBase("address", "internalid1", typeof(BundledAssetProvider).FullName, typeof(GameObject));

            Assert.IsTrue(m_Addressables.Equals(loc1, loc2));
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalog_UpdatesCachedData_IfHashFileUpdates()
        {
            yield return Init();
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalog_UpdatesCachedData_IfHashFileUpdates)} due to missing CatalogLocation.");
                yield break;
            }

            Directory.CreateDirectory(kCatalogFolderPath);
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            string cachedDataPath = m_Addressables.ResolveInternalId(AddressablesImpl.kCacheDataFolder + Path.GetFileName(kCatalogRemotePath));
            string cachedHashPath = cachedDataPath.Replace(".json", ".hash");
            string remoteHashPath = WriteHashFileForCatalog(fullRemotePath, "123");

            string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
            if (baseCatalogPath.StartsWith("file://"))
                baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
            File.Copy(baseCatalogPath, fullRemotePath);

            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;
            m_Addressables.Release(op1);

            Assert.IsTrue(File.Exists(cachedDataPath));
            Assert.IsTrue(File.Exists(cachedHashPath));
            Assert.AreEqual("123", File.ReadAllText(cachedHashPath));

            remoteHashPath = WriteHashFileForCatalog(fullRemotePath, "456");

            var op2 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op2;

            Assert.AreEqual("456", File.ReadAllText(cachedHashPath));

            m_Addressables.Release(op2);
            Directory.Delete(kCatalogFolderPath, true);
            File.Delete(cachedDataPath);
            File.Delete(cachedHashPath);
        }

        [UnityTest]
        public IEnumerator LoadingContentCatalog_NoCacheDataCreated_IfRemoteHashDoesntExist()
        {
            yield return Init();
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
                UnityEngine.Debug.Log($"Skipping test {nameof(LoadingContentCatalog_NoCacheDataCreated_IfRemoteHashDoesntExist)} due to missing CatalogLocation.");
                yield break;
            }

            Directory.CreateDirectory(kCatalogFolderPath);
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);
            string cachedDataPath = m_Addressables.ResolveInternalId(AddressablesImpl.kCacheDataFolder + Path.GetFileName(kCatalogRemotePath));
            string cachedHashPath = cachedDataPath.Replace(".json", ".hash");

            string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
            if (baseCatalogPath.StartsWith("file://"))
                baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
            File.Copy(baseCatalogPath, fullRemotePath);

            var op1 = m_Addressables.LoadContentCatalogAsync(fullRemotePath, false);
            yield return op1;
            m_Addressables.Release(op1);

            Assert.IsFalse(File.Exists(cachedDataPath));
            Assert.IsFalse(File.Exists(cachedHashPath));

            Directory.Delete(kCatalogFolderPath, true);
        }

        [UnityTest]
        public IEnumerator ContentCatalogData_IsCleared_WhenInitializationOperationLoadContentCatalogOp_IsReleased()
        {
            yield return Init();
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
                UnityEngine.Debug.Log($"Skipping test {nameof(ContentCatalogData_IsCleared_WhenInitializationOperationLoadContentCatalogOp_IsReleased)} due to missing CatalogLocation.");
                yield break;
            }

            string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;

            if (baseCatalogPath.StartsWith("file://"))
                baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;

            var location = m_Addressables.CreateCatalogLocationWithHashDependencies(baseCatalogPath, string.Empty);
            var loadCatalogHandle = InitializationOperation.LoadContentCatalog(m_Addressables, location, string.Empty);

            yield return loadCatalogHandle;
            ContentCatalogProvider ccp = m_Addressables.ResourceManager.ResourceProviders
                .FirstOrDefault(rp => rp.GetType() == typeof(ContentCatalogProvider)) as ContentCatalogProvider;

            var ccd = ccp.m_LocationToCatalogLoadOpMap[location].m_ContentCatalogData;
            Assert.IsFalse(CatalogDataWasCleaned(ccd));

            loadCatalogHandle.Release();

            Assert.IsTrue(CatalogDataWasCleaned(ccd));

            PostTearDownEvent = ResetAddressables;
        }

        internal bool CatalogDataWasCleaned(ContentCatalogData data)
        {
            return string.IsNullOrEmpty(data.m_KeyDataString) &&
                string.IsNullOrEmpty(data.m_BucketDataString) &&
                string.IsNullOrEmpty(data.m_EntryDataString) &&
                string.IsNullOrEmpty(data.m_ExtraDataString) &&
                data.m_InternalIds == null &&
                string.IsNullOrEmpty(data.m_LocatorId) &&
                data.m_ProviderIds == null &&
                data.m_ResourceProviderData == null &&
                data.m_resourceTypes == null;
        }

        [UnityTest]
        public IEnumerator ContentCatalogData_IsCleared_ForCorrectCatalogLoadOp_WhenOpIsReleased()
        {
            yield return Init();
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
                UnityEngine.Debug.Log($"Skipping test {nameof(ContentCatalogData_IsCleared_ForCorrectCatalogLoadOp_WhenOpIsReleased)} due to missing CatalogLocation.");
                yield break;
            }

            Directory.CreateDirectory(kCatalogFolderPath);
            string fullRemotePath = Path.Combine(kCatalogFolderPath, kCatalogRemotePath);

            string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
            if (baseCatalogPath.StartsWith("file://"))
                baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;
            File.Copy(baseCatalogPath, fullRemotePath);

            var location = m_Addressables.CreateCatalogLocationWithHashDependencies(baseCatalogPath, string.Empty);
            var location2 = m_Addressables.CreateCatalogLocationWithHashDependencies(fullRemotePath, fullRemotePath.Replace(".json", ".hash"));
            var loadCatalogHandle = InitializationOperation.LoadContentCatalog(m_Addressables, location, string.Empty);
            yield return loadCatalogHandle;
            var loadCatalogHandle2 = InitializationOperation.LoadContentCatalog(m_Addressables, location2, string.Empty);
            yield return loadCatalogHandle2;

            ContentCatalogProvider ccp = m_Addressables.ResourceManager.ResourceProviders
                .FirstOrDefault(rp => rp.GetType() == typeof(ContentCatalogProvider)) as ContentCatalogProvider;

            var ccd = ccp.m_LocationToCatalogLoadOpMap[location].m_ContentCatalogData;
            var ccd2 = ccp.m_LocationToCatalogLoadOpMap[location2].m_ContentCatalogData;

            Assert.IsFalse(CatalogDataWasCleaned(ccd));
            Assert.IsFalse(CatalogDataWasCleaned(ccd2));

            loadCatalogHandle.Release();

            Assert.IsTrue(CatalogDataWasCleaned(ccd));
            Assert.IsFalse(CatalogDataWasCleaned(ccd2));

            Directory.Delete(kCatalogFolderPath, true);
            loadCatalogHandle2.Release();

            PostTearDownEvent = ResetAddressables;
        }

        [UnityTest]
        public IEnumerator ContentCatalogProvider_RemovesEntryFromMap_WhenOperationHandleReleased()
        {
            yield return Init();
            if (m_Addressables.m_ResourceLocators[0].CatalogLocation == null)
            {
                UnityEngine.Debug.Log($"Skipping test {nameof(ContentCatalogProvider_RemovesEntryFromMap_WhenOperationHandleReleased)} due to missing CatalogLocation.");
                yield break;
            }
            string baseCatalogPath = m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId;
            if (baseCatalogPath.StartsWith("file://"))
                baseCatalogPath = new Uri(m_Addressables.m_ResourceLocators[0].CatalogLocation.InternalId).AbsolutePath;

            var handle = m_Addressables.LoadContentCatalogAsync(baseCatalogPath, false);
            yield return handle;

            ContentCatalogProvider ccp = m_Addressables.ResourceManager.ResourceProviders
                .FirstOrDefault(rp => rp.GetType() == typeof(ContentCatalogProvider)) as ContentCatalogProvider;

            Assert.AreEqual(1, ccp.m_LocationToCatalogLoadOpMap.Count);

            handle.Release();

            Assert.AreEqual(0, ccp.m_LocationToCatalogLoadOpMap.Count);

            PostTearDownEvent = ResetAddressables;
        }

        [UnityTest]
        public IEnumerator VerifyProfileVariableEvaluation()
        {
            yield return Init();
            Assert.AreEqual(string.Format("{0}", m_Addressables.RuntimePath), AddressablesRuntimeProperties.EvaluateString("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"));
        }

        [UnityTest]
        public IEnumerator VerifyDownloadSize()
        {
            yield return Init();
            long expectedSize = 0;
            var locMap = new ResourceLocationMap("TestLocator");

            var bundleLoc1 = new ResourceLocationBase("sizeTestBundle1", "http://nowhere.com/mybundle1.bundle", typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData1 = (bundleLoc1.Data = CreateLocationSizeData("sizeTestBundle1", 1000, 123, "hashstring1")) as ILocationSizeData;
            if (sizeData1 != null)
                expectedSize += sizeData1.ComputeSize(bundleLoc1, null);

            var bundleLoc2 = new ResourceLocationBase("sizeTestBundle2", "http://nowhere.com/mybundle2.bundle", typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData2 = (bundleLoc2.Data = CreateLocationSizeData("sizeTestBundle2", 500, 123, "hashstring2")) as ILocationSizeData;
            if (sizeData2 != null)
                expectedSize += sizeData2.ComputeSize(bundleLoc2, null);

            var assetLoc = new ResourceLocationBase("sizeTestAsset", "myASset.asset", typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc1, bundleLoc2);

            locMap.Add("sizeTestBundle1", bundleLoc1);
            locMap.Add("sizeTestBundle2", bundleLoc2);
            locMap.Add("sizeTestAsset", assetLoc);
            m_Addressables.AddResourceLocator(locMap);

            var dOp = m_Addressables.GetDownloadSizeAsync((object)"sizeTestAsset");
            yield return dOp;
            Assert.AreEqual(expectedSize, dOp.Result);
            dOp.Release();
        }

        [UnityTest]
        public IEnumerator GetDownloadSize_CalculatesCachedBundles()
        {
#if ENABLE_CACHING
            yield return Init();
            long expectedSize = 0;
            long bundleSize1 = 1000;
            long bundleSize2 = 500;
            var locMap = new ResourceLocationMap("TestLocator");

            Caching.ClearCache();
            //Simulating a cached bundle
            string fakeCachePath = CreateFakeCachedBundle("cachedSizeTestBundle1", "be38e35d2177c282d5d6a2e54a803aab");

            var bundleLoc1 = new ResourceLocationBase("cachedSizeTestBundle1", "http://nowhere.com/GetDownloadSize_CalculatesCachedBundlesBundle1.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData1 =
                (bundleLoc1.Data = CreateLocationSizeData("cachedSizeTestBundle1", bundleSize1, 123,
                    "be38e35d2177c282d5d6a2e54a803aab")) as ILocationSizeData;
            if (sizeData1 != null)
                expectedSize += sizeData1.ComputeSize(bundleLoc1, null);

            var bundleLoc2 = new ResourceLocationBase("sizeTestBundle2", "http://nowhere.com/GetDownloadSize_CalculatesCachedBundlesBundle2.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData2 =
                (bundleLoc2.Data = CreateLocationSizeData("sizeTestBundle2", bundleSize2, 123,
                    "d9fe965a6b253fb9dbd3e1cb08b7d66f")) as ILocationSizeData;
            if (sizeData2 != null)
                expectedSize += sizeData2.ComputeSize(bundleLoc2, null);

            var assetLoc = new ResourceLocationBase("cachedSizeTestAsset", "myASset.asset",
                typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc1, bundleLoc2);

            locMap.Add("cachedSizeTestBundle1", bundleLoc1);
            locMap.Add("cachedSizeTestBundle2", bundleLoc2);
            locMap.Add("cachedSizeTestAsset", assetLoc);
            m_Addressables.AddResourceLocator(locMap);

            var dOp = m_Addressables.GetDownloadSizeAsync((object)"cachedSizeTestAsset");
            yield return dOp;
            Assert.IsTrue((bundleSize1 + bundleSize2) > dOp.Result);
            Assert.AreEqual(expectedSize, dOp.Result);

            dOp.Release();
            m_Addressables.RemoveResourceLocator(locMap);
            Directory.Delete(fakeCachePath, true);
#else
            Assert.Ignore();
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GetDownloadSize_WithList_CalculatesCachedBundles()
        {
#if ENABLE_CACHING
            yield return Init();
            long expectedSize = 0;
            long bundleSize1 = 1000;
            long bundleSize2 = 500;
            var locMap = new ResourceLocationMap("TestLocator");

            Assert.IsTrue(Caching.ClearCache(), "Was unable to clear the cache.  Test results are affected");
            //Simulating a cached bundle
            string fakeCachePath = CreateFakeCachedBundle("cachedSizeTestBundle1", "0e38e35d2177c282d5d6a2e54a803aab");

            var bundleLoc1 = new ResourceLocationBase("cachedSizeTestBundle1", "http://nowhere.com/GetDownloadSize_WithList_CalculatesCachedBundlesBundle1.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData1 =
                (bundleLoc1.Data = CreateLocationSizeData("cachedSizeTestBundle1", bundleSize1, 123,
                    "0e38e35d2177c282d5d6a2e54a803aab")) as ILocationSizeData;
            if (sizeData1 != null)
                expectedSize += sizeData1.ComputeSize(bundleLoc1, null);

            var bundleLoc2 = new ResourceLocationBase("sizeTestBundle2", "http://nowhere.com/GetDownloadSize_WithList_CalculatesCachedBundlesBundle2.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData2 =
                (bundleLoc2.Data = CreateLocationSizeData("sizeTestBundle2", bundleSize2, 123,
                    "09fe965a6b253fb9dbd3e1cb08b7d66f")) as ILocationSizeData;
            if (sizeData2 != null)
                expectedSize += sizeData2.ComputeSize(bundleLoc2, null);

            var assetLoc = new ResourceLocationBase("cachedSizeTestAsset", "myASset.asset",
                typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc1, bundleLoc2);

            locMap.Add("cachedSizeTestBundle1", bundleLoc1);
            locMap.Add("cachedSizeTestBundle2", bundleLoc2);
            locMap.Add("cachedSizeTestAsset", assetLoc);
            m_Addressables.AddResourceLocator(locMap);

            var dOp = m_Addressables.GetDownloadSizeAsync(new List<object>()
            {
                "cachedSizeTestAsset",
                bundleLoc1,
                bundleLoc2
            }
            );
            yield return dOp;
            Assert.IsTrue((bundleSize1 + bundleSize2) > dOp.Result);
            Assert.AreEqual(expectedSize, dOp.Result);

            dOp.Release();
            m_Addressables.RemoveResourceLocator(locMap);
            Directory.Delete(fakeCachePath, true);
#else
            Assert.Ignore();
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GetDownloadSize_WithList_CalculatesCorrectSize_WhenAssetsReferenceSameBundle()
        {
#if ENABLE_CACHING
            yield return Init();
            long bundleSize1 = 1000;
            long expectedSize = 0;

            var bundleLoc1 = new ResourceLocationBase("sizeTestBundle1", "http://nowhere.com/GetDownloadSize_WithList_CalculatesCachedBundlesBundle1.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData1 =
                (bundleLoc1.Data = CreateLocationSizeData("cachedSizeTestBundle1", bundleSize1, 123,
                    "0e38e35d2177c282d5d6a2e54a803aab")) as ILocationSizeData;
            if (sizeData1 != null)
                expectedSize += sizeData1.ComputeSize(bundleLoc1, null);

            var assetLoc1 = new ResourceLocationBase("cachedSizeTestAsset1", "myAsset1.asset",
                typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc1);

            var assetLoc2 = new ResourceLocationBase("cachedSizeTestAsset2", "myAsset2.asset",
                typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc1);

            var dOp = m_Addressables.GetDownloadSizeAsync(new List<object>()
            {
                assetLoc1,
                assetLoc2
            }
            );
            yield return dOp;
            Assert.IsTrue(bundleSize1 >= dOp.Result);
            Assert.AreEqual(expectedSize, dOp.Result);
#else
            Assert.Ignore();
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GetDownloadSize_WithList_CalculatesCorrectSize_WhenAssetsReferenceDifferentBundle()
        {
#if ENABLE_CACHING
            yield return Init();
            long bundleSize1 = 1000;
            long bundleSize2 = 250;
            long expectedSize = 0;

            var bundleLoc1 = new ResourceLocationBase("sizeTestBundle1", "http://nowhere.com/GetDownloadSize_WithList_CalculatesCachedBundlesBundle1.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData1 =
                (bundleLoc1.Data = CreateLocationSizeData("cachedSizeTestBundle1", bundleSize1, 123,
                    "0e38e35d2177c282d5d6a2e54a803aab")) as ILocationSizeData;
            if (sizeData1 != null)
                expectedSize += sizeData1.ComputeSize(bundleLoc1, null);

            var bundleLoc2 = new ResourceLocationBase("cachedSizeTestBundle2", "http://nowhere.com/GetDownloadSize_WithList_CalculatesCachedBundlesBundle2.bundle",
                typeof(AssetBundleProvider).FullName, typeof(object));
            var sizeData2 =
                (bundleLoc2.Data = CreateLocationSizeData("cachedSizeTestBundle2", bundleSize2, 123,
                    "09fe965a6b253fb9dbd3e1cb08b7d66f")) as ILocationSizeData;
            if (sizeData2 != null)
                expectedSize += sizeData2.ComputeSize(bundleLoc2, null);

            var assetLoc1 = new ResourceLocationBase("cachedSizeTestAsset1", "myAsset1.asset",
                typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc1);

            var assetLoc2 = new ResourceLocationBase("cachedSizeTestAsset2", "myAsset2.asset",
                typeof(BundledAssetProvider).FullName, typeof(object), bundleLoc2);

            var dOp = m_Addressables.GetDownloadSizeAsync(new List<object>()
            {
                assetLoc1,
                assetLoc2
            }
            );
            yield return dOp;
            Assert.IsTrue((bundleSize1 + bundleSize2) >= dOp.Result);
            Assert.AreEqual(expectedSize, dOp.Result);
#else
            Assert.Ignore();
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsWithCorrectKeyAndWrongTypeReturnsEmptyResult()
        {
            yield return Init();
            AsyncOperationHandle<IList<IResourceLocation>> op = m_Addressables.LoadResourceLocationsAsync("prefabs_evenBASE", typeof(Texture2D));
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(op.Result.Count, 0);
            op.Release();
        }

        [UnityTest]
        public IEnumerator CanGetResourceLocationsWithSingleKey()
        {
            yield return Init();
            int loadCount = 0;
            int loadedCount = 0;
            var ops = new List<AsyncOperationHandle<IList<IResourceLocation>>>();
            foreach (var k in m_KeysHashSet)
            {
                loadCount++;
                AsyncOperationHandle<IList<IResourceLocation>> op = m_Addressables.LoadResourceLocationsAsync(k.Key, typeof(object));
                ops.Add(op);
                op.Completed += op2 =>
                {
                    loadedCount++;
                    Assert.IsNotNull(op2.Result);
                    Assert.AreEqual(k.Value, op2.Result.Count);
                };
            }
            foreach (var op in ops)
            {
                yield return op;
                op.Release();
            }
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsMergeModesFailsWithNoKeys([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)] Addressables.MergeMode mode)
        {
            yield return Init();

            IList<IResourceLocation> results;
            var ret = m_Addressables.GetResourceLocations(new object[] {}, typeof(GameObject), mode, out results);
            Assert.IsFalse(ret);
            Assert.IsNull(results);
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsMergeModesSucceedsWithSingleKey([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)] Addressables.MergeMode mode)
        {
            yield return Init();

            IList<IResourceLocation> results;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), mode, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsMergeModeUnionSucceedsWithValidKeys()
        {
            yield return Init();

            IList<IResourceLocation> results;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
            var evenCount = results.Count;

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_oddBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
            var oddCount = results.Count;

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE", "prefabs_oddBASE" }, typeof(GameObject), Addressables.MergeMode.Union, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
            Assert.AreEqual(oddCount + evenCount, results.Count);
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsMergeModeUnionSucceedsWithInvalidKeys()
        {
            yield return Init();

            IList<IResourceLocation> results;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
            var evenCount = results.Count;

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_oddBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
            var oddCount = results.Count;

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE", "prefabs_oddBASE", "INVALIDKEY" }, typeof(GameObject), Addressables.MergeMode.Union, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);
            Assert.AreEqual(oddCount + evenCount, results.Count);
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsMergeModeIntersectionFailsIfNoResultsDueToIntersection()
        {
            yield return Init();

            IList<IResourceLocation> results;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_oddBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE", "prefabs_oddBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsFalse(ret);
            Assert.IsNull(results);
        }

        [UnityTest]
        public IEnumerator GetResourceLocationsMergeModeIntersectionFailsIfNoResultsDueToInvalidKey()
        {
            yield return Init();

            IList<IResourceLocation> results;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_oddBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsTrue(ret);
            Assert.NotNull(results);
            Assert.GreaterOrEqual(results.Count, 1);

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE", "prefabs_oddBASE", "INVALIDKEY" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsFalse(ret);
            Assert.IsNull(results);

            ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE", "INVALIDKEY" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsFalse(ret);
            Assert.IsNull(results);

            ret = m_Addressables.GetResourceLocations(new object[] { "INVALIDKEY" }, typeof(GameObject), Addressables.MergeMode.Intersection, out results);
            Assert.IsFalse(ret);
            Assert.IsNull(results);
        }

        [UnityTest]
        public IEnumerator WhenLoadWithInvalidKey_ReturnedOpIsFailed()
        {
            yield return Init();
            List<object> keys = new List<object>() { "INVALID1", "INVALID2" };
            AsyncOperationHandle<IList<GameObject>> gop = new AsyncOperationHandle<IList<GameObject>>();
            using (new IgnoreFailingLogMessage())
            {
                gop = m_Addressables.LoadAssetsAsync<GameObject>(keys, null, Addressables.MergeMode.Intersection, true);
            }

            while (!gop.IsDone)
                yield return null;
            Assert.IsTrue(gop.IsDone);
            Assert.AreEqual(AsyncOperationStatus.Failed, gop.Status);
            m_Addressables.Release(gop);
        }

        [UnityTest]
        public IEnumerator CanLoadAssetsWithMultipleKeysMerged()
        {
            yield return Init();
            List<object> keys = new List<object>() { AddressablesTestUtility.GetPrefabLabel("BASE"), AddressablesTestUtility.GetPrefabUniqueLabel("BASE", 0) };
            AsyncOperationHandle<IList<GameObject>> gop = m_Addressables.LoadAssetsAsync<GameObject>(keys, null, Addressables.MergeMode.Intersection, true);
            while (!gop.IsDone)
                yield return null;
            Assert.IsTrue(gop.IsDone);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, gop.Status);
            Assert.NotNull(gop.Result);
            Assert.AreEqual(1, gop.Result.Count);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, gop.Status);
            m_Addressables.Release(gop);
        }

        [UnityTest]
        public IEnumerator Release_WhenObjectIsUnknown_LogsErrorAndDoesNotDestroy()
        {
            yield return Init();
            GameObject go = Object.Instantiate(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = "TestCube";

            m_Addressables.Release(go);
            LogAssert.Expect(LogType.Error, new Regex("Addressables.Release was called on.*"));
            yield return null;

            GameObject foundObj = GameObject.Find("TestCube");
            Assert.IsNotNull(foundObj);
            Object.Destroy(foundObj);
        }

        [UnityTest]
        public IEnumerator ReleaseInstance_WhenObjectIsUnknown_LogsErrorAndDestroys()
        {
            yield return Init();
            GameObject go = Object.Instantiate(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = "TestCube";

            Assert.IsFalse(m_Addressables.ReleaseInstance(go));
        }

        [UnityTest]
        public IEnumerator LoadAsset_WhenEntryExists_ReturnsAsset()
        {
            yield return Init();
            string label = AddressablesTestUtility.GetPrefabUniqueLabel("BASE", 0);
            AsyncOperationHandle<GameObject> op = m_Addressables.LoadAssetAsync<GameObject>(label);
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.IsTrue(op.Result != null);
            op.Release();
        }

        [UnityTest]
        public IEnumerator LoadAssetWithWrongType_WhenEntryExists_Fails()
        {
            yield return Init();
            string label = AddressablesTestUtility.GetPrefabUniqueLabel("BASE", 0);
            AsyncOperationHandle<Texture> op = new AsyncOperationHandle<Texture>();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.LoadAssetAsync<Texture>(label);
                yield return op;
            }
            Assert.AreEqual(AsyncOperationStatus.Failed, op.Status);
            Assert.IsNull(op.Result);
            op.Release();
        }

        [UnityTest]
        public IEnumerator LoadAsset_WhenEntryDoesNotExist_OperationFails()
        {
            yield return Init();
            AsyncOperationHandle<GameObject> op = new AsyncOperationHandle<GameObject>();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.LoadAssetAsync<GameObject>("unknownlabel");
            }
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Failed, op.Status);
            Assert.IsTrue(op.Result == null);
            op.Release();
        }

        [UnityTest]
        public IEnumerator LoadAsset_CanReleaseThroughAddressablesInCallback([Values(true, false)] bool addressableRelease)
        {
            yield return Init();
            var op = m_Addressables.LoadAssetAsync<object>(m_PrefabKeysList[0]);
            op.Completed += x =>
            {
                Assert.IsNotNull(x.Result);
                if (addressableRelease)
                    m_Addressables.Release(x.Result);
                else
                    op.Release();
            };
            yield return op;
        }

        [UnityTest]
        public IEnumerator LoadAsset_WhenPrefabLoadedAsMultipleTypes_ResultIsEqual()
        {
            yield return Init();

            string label = AddressablesTestUtility.GetPrefabUniqueLabel("BASE", 0);
            AsyncOperationHandle<object> op1 = m_Addressables.LoadAssetAsync<object>(label);
            AsyncOperationHandle<GameObject> op2 = m_Addressables.LoadAssetAsync<GameObject>(label);
            yield return op1;
            yield return op2;
            Assert.AreEqual(op1.Result, op2.Result);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op2.Status);
            op1.Release();
            op2.Release();
        }

        [UnityTest]
        public IEnumerator LoadAssets_InvokesCallbackPerAsset()
        {
            yield return Init();
            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            HashSet<GameObject> ops = new HashSet<GameObject>();
            var gop = m_Addressables.LoadAssetsAsync<GameObject>(label, x => { ops.Add(x); }, true);
            yield return gop;
            Assert.AreEqual(AddressablesTestUtility.kPrefabCount, ops.Count);
            for (int i = 0; i < ops.Count; i++)
                Assert.IsTrue(ops.Contains(gop.Result[i]));
            gop.Release();
        }

        // TODO: this doesn't actually check that something was downloaded. It is more: can load dependencies.
        // We really need to address the downloading feature
        [UnityTest]
        public IEnumerator DownloadDependnecies_CanDownloadDependencies()
        {
            yield return Init();
            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(label);
            yield return op;
            op.Release();
        }

        [UnityTest]
        public IEnumerator DownloadDependnecies_AutoReleaseHandle_ReleasesOnCompletion()
        {
            yield return Init();
            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(label, true);
            yield return op;
            Assert.IsFalse(op.IsValid());
        }

        [UnityTest]
        public IEnumerator DownloadDependneciesWithAddress_AutoReleaseHandle_ReleasesOnCompletion()
        {
            yield return Init();
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(m_PrefabKeysList[0], true);
            yield return op;
            Assert.IsFalse(op.IsValid());
        }

        [UnityTest]
        public IEnumerator DownloadDependnecies_DoesNotRetainLoadedBundles_WithAutoRelease()
        {
            yield return Init();
            int bundleCountBefore = AssetBundle.GetAllLoadedAssetBundles().Count();
            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(label, true);
            yield return op;
            Assert.AreEqual(bundleCountBefore, AssetBundle.GetAllLoadedAssetBundles().Count());
        }

        [UnityTest]
        public IEnumerator DownloadDependencies_ReturnsValidTask()
        {
            yield return Init();
            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(label);

            Assert.IsNotNull(op.Task);
            yield return op;
            Assert.IsNotNull(op.Task);

            op.Release();
        }

        [UnityTest]
        public IEnumerator StressInstantiation()
        {
            yield return Init();

            // TODO: move this safety check to test fixture base
            var objs = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in objs)
                Assert.False(r.name.EndsWith("(Clone)"), "All instances from previous test were not cleaned up");

            var ops = new List<AsyncOperationHandle<GameObject>>();
            for (int i = 0; i < 50; i++)
            {
                var key = m_PrefabKeysList[i % m_PrefabKeysList.Count];
                ops.Add(m_Addressables.InstantiateAsync(key));
            }

            foreach (AsyncOperationHandle<GameObject> op in ops)
                yield return op;

            foreach (AsyncOperationHandle<GameObject> op in ops)
                m_Addressables.ReleaseInstance(op.Result);

            yield return null;

            objs = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in objs)
                Assert.False(r.name.EndsWith("(Clone)"), "All instances from this test were not cleaned up");
        }

        [UnityTest]
        public IEnumerator AssetReference_HandleIsInvalidated_WhenReleasingLoadOperation()
        {
            yield return Init();
            AsyncOperationHandle handle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return handle;
            Assert.IsNotNull(handle.Result as GameObject);
            AssetReferenceTestBehavior behavior =
                (handle.Result as GameObject).GetComponent<AssetReferenceTestBehavior>();

            using (new IgnoreFailingLogMessage())
                yield return behavior.Reference.LoadAssetAsync<GameObject>();

            AsyncOperationHandle referenceHandle = behavior.Reference.OperationHandle;
            Assert.IsTrue(behavior.Reference.IsValid());
            m_Addressables.Release(referenceHandle);
            yield return referenceHandle;
            Assert.IsFalse(behavior.Reference.IsValid());

            handle.Release();
        }

        [UnityTest]
        public IEnumerator CanUnloadAssetReference_WithAddressables()
        {
            yield return Init();

            AsyncOperationHandle handle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return handle;
            Assert.IsNotNull(handle.Result as GameObject);
            AssetReferenceTestBehavior behavior =
                (handle.Result as GameObject).GetComponent<AssetReferenceTestBehavior>();
            AsyncOperationHandle<GameObject> assetRefHandle = m_Addressables.InstantiateAsync(behavior.Reference);
            yield return assetRefHandle;
            Assert.IsNotNull(assetRefHandle.Result);

            string name = assetRefHandle.Result.name;
            Assert.IsNotNull(GameObject.Find(name));

            m_Addressables.ReleaseInstance(assetRefHandle.Result);
            yield return null;
            Assert.IsNull(GameObject.Find(name));

            handle.Release();
        }

        [UnityTest]
        public IEnumerator CanloadAssetReferenceSubObject()
        {
            yield return Init();

            var handle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return handle;
            Assert.IsNotNull(handle.Result);
            AssetReferenceTestBehavior behavior = handle.Result.GetComponent<AssetReferenceTestBehavior>();

            AsyncOperationHandle<Object> assetRefHandle = m_Addressables.LoadAssetAsync<Object>(behavior.ReferenceWithSubObject);
            yield return assetRefHandle;
            Assert.IsNotNull(assetRefHandle.Result);
            m_Addressables.Release(assetRefHandle);
            handle.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesIntegration_LoadAssetAsync_CanLoadAssetReferenceObjectList()
        {
            yield return Init();

            var assetRefHandle = m_Addressables.LoadAssetAsync<Object[]>("assetWithSubObjects");
            yield return assetRefHandle;
            Assert.IsNotNull(assetRefHandle.Result);
            Assert.AreEqual(assetRefHandle.Result.Length, 2);
            Assert.AreEqual(assetRefHandle.Result[0].name, "assetWithSubObjects");
            Assert.AreEqual(assetRefHandle.Result[1].name, "sub-shown");
            m_Addressables.Release(assetRefHandle);
        }

        [UnityTest]
        public IEnumerator LoadAssets_WithHiddenSubObjects_OnlyReturnsNonHidden_WithMainAssetFirst()
        {
            yield return Init();

            var handle = m_Addressables.LoadAssetAsync<IList<Object>>("assetWithSubObjects");
            yield return handle;
            Assert.IsNotNull(handle.Result);
            Assert.AreEqual(handle.Result.Count, 2);
            Assert.AreEqual(handle.Result[0].name, "assetWithSubObjects");
            Assert.AreEqual(handle.Result[1].name, "sub-shown");
            handle.Release();
        }

        [UnityTest]
        public IEnumerator RuntimeKeyIsValid_ReturnsTrueForSubObjects()
        {
            yield return Init();

            var handle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return handle;
            Assert.IsNotNull(handle.Result);
            AssetReferenceTestBehavior behavior = handle.Result.GetComponent<AssetReferenceTestBehavior>();

            Assert.IsTrue(behavior.ReferenceWithSubObject.RuntimeKeyIsValid());

            handle.Release();
        }

        [UnityTest]
        public IEnumerator RuntimeKeyIsValid_ReturnsTrueForValidKeys()
        {
            yield return Init();

            AsyncOperationHandle handle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return handle;
            Assert.IsNotNull(handle.Result as GameObject);
            AssetReferenceTestBehavior behavior =
                (handle.Result as GameObject).GetComponent<AssetReferenceTestBehavior>();

            Assert.IsTrue((behavior.Reference as IKeyEvaluator).RuntimeKeyIsValid());
            Assert.IsTrue((behavior.LabelReference as IKeyEvaluator).RuntimeKeyIsValid());

            handle.Release();
        }

        [UnityTest]
        public IEnumerator PercentComplete_CalculationIsCorrect_WhenInAGroupOperation()
        {
            yield return Init();
            GroupOperation groupOp = new GroupOperation();

            float handle1PercentComplete = 0.22f;
            float handle2PercentComplete = 0.78f;
            float handle3PercentComplete = 1.0f;
            float handle4PercentComplete = 0.35f;

            List<AsyncOperationHandle> handles = new List<AsyncOperationHandle>()
            {
                new ManualPercentCompleteOperation(handle1PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle2PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle3PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle4PercentComplete).Handle
            };

            groupOp.Init(handles);

            Assert.AreEqual((handle1PercentComplete + handle2PercentComplete + handle3PercentComplete + handle4PercentComplete) / 4, groupOp.PercentComplete);
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_SumDependencyNameHashCodes_ProperlyCalculatesForOneLayerOfDependencies()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            GroupOperation groupOp = new GroupOperation();


            float handle1PercentComplete = 0.22f;
            float handle2PercentComplete = 0.78f;
            float handle3PercentComplete = 1.0f;
            float handle4PercentComplete = 0.35f;

            List<AsyncOperationHandle> handles = new List<AsyncOperationHandle>()
            {
                new ManualPercentCompleteOperation(handle1PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle2PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle3PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle4PercentComplete).Handle
            };

            groupOp.Init(handles);

            var handle = groupOp.Handle;
            var dependencyNameHashSum = handle.DebugName.GetHashCode() + rmd.SumDependencyNameHashCodes(handle);
            var manualDepNameHashSum = handle.DebugName.GetHashCode();
            foreach (var h in handles)
                manualDepNameHashSum += h.DebugName.GetHashCode();
            Assert.AreEqual(manualDepNameHashSum, dependencyNameHashSum, "Calculation of hashcode was not completed as expected.");
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_SumDependencyNameHashCodes_ProperlyCalculatesForMultipleLayersOfDependencies()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            GroupOperation groupOp = new GroupOperation();
            GroupOperation embeddedOp = new GroupOperation();


            float handle1PercentComplete = 0.22f;
            float handle2PercentComplete = 0.78f;
            float handle3PercentComplete = 1.0f;
            float handle4PercentComplete = 0.35f;

            List<AsyncOperationHandle> embeddedHandles = new List<AsyncOperationHandle>()
            {
                new ManualPercentCompleteOperation(handle1PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle2PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle3PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle4PercentComplete).Handle
            };

            embeddedOp.Init(embeddedHandles);

            List<AsyncOperationHandle> handles = new List<AsyncOperationHandle>()
            {
                embeddedOp.Handle,
                new ManualPercentCompleteOperation(handle1PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle2PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle3PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle4PercentComplete).Handle
            };

            groupOp.Init(handles);

            var dependencyNameHashSum = groupOp.Handle.DebugName.GetHashCode() + rmd.SumDependencyNameHashCodes(groupOp.Handle);
            int manualDepNameHashSum;

            unchecked
            {
                manualDepNameHashSum = groupOp.Handle.DebugName.GetHashCode();
                foreach (var h in handles)
                    manualDepNameHashSum += h.DebugName.GetHashCode();
                foreach (var h in embeddedHandles)
                    manualDepNameHashSum += h.DebugName.GetHashCode();
            }

            Assert.AreEqual(dependencyNameHashSum, manualDepNameHashSum, "Calculation of hashcode was not completed as expected.");
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_CalculateHashCode_NonChangingNameCase()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            GroupOperation groupOp = new GroupOperation();


            float handle1PercentComplete = 0.22f;
            float handle2PercentComplete = 0.78f;
            float handle3PercentComplete = 1.0f;
            float handle4PercentComplete = 0.35f;

            List<AsyncOperationHandle> handles = new List<AsyncOperationHandle>()
            {
                new ManualPercentCompleteOperation(handle1PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle2PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle3PercentComplete).Handle,
                new ManualPercentCompleteOperation(handle4PercentComplete).Handle
            };

            groupOp.Init(handles);

            var handle = groupOp.Handle;
            var dependencyNameHashSum = rmd.CalculateHashCode(handle);
            var manualDepNameHashSum = handle.DebugName.GetHashCode();
            foreach (var h in handles)
                manualDepNameHashSum += h.DebugName.GetHashCode();
            Assert.AreEqual(manualDepNameHashSum, dependencyNameHashSum, "Calculation of hashcode was not completed as expected.");
        }

        private class DebugNameTestOperation : AsyncOperationBase<string>
        {
            string m_DebugName;
            List<AsyncOperationHandle> m_Dependencies;
            protected override void Execute()
            {
            }

            internal DebugNameTestOperation(string debugName)
            {
                m_DebugName = debugName;
                m_Dependencies = new List<AsyncOperationHandle>();
            }

            internal DebugNameTestOperation(string debugName, List<AsyncOperationHandle> deps)
            {
                m_DebugName = debugName;
                m_Dependencies = deps;
            }

            protected override void GetDependencies(List<AsyncOperationHandle> dependencies)
            {
                foreach (var handle in m_Dependencies)
                    dependencies.Add(handle);
            }

            protected override string DebugName
            {
                get { return m_DebugName;}
            }
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_CalculateHashCode_NameChangingCase()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            AsyncOperationHandle changingHandle = new ManualPercentCompleteOperation(0.22f).Handle;

            Assert.AreEqual(changingHandle.GetHashCode(), rmd.CalculateHashCode(changingHandle), "Default hashcode should have been used since ManualPercentCompleteOperation includes its status in its DebugName");
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_CalculateHashCode_SameNameGivesSameHashcode()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            DebugNameTestOperation op1 = new DebugNameTestOperation("Same name");
            AsyncOperationHandle handle1 = new AsyncOperationHandle(op1);

            DebugNameTestOperation op2 = new DebugNameTestOperation("Same name");
            AsyncOperationHandle handle2 = new AsyncOperationHandle(op2);

            Assert.AreEqual(rmd.CalculateHashCode(handle1), rmd.CalculateHashCode(handle2), "Two separate handles with the same DebugName should have the same hashcode. ");
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_CalculateHashCode_SimilarNameGivesDifHashcode()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            DebugNameTestOperation op1 = new DebugNameTestOperation("Same name");
            AsyncOperationHandle handle1 = new AsyncOperationHandle(op1);

            DebugNameTestOperation op2 = new DebugNameTestOperation("SaMe name");
            AsyncOperationHandle handle2 = new AsyncOperationHandle(op2);

            Assert.AreNotEqual(rmd.CalculateHashCode(handle1), rmd.CalculateHashCode(handle2), "Two similar, but different names should have different hashcodes. ");
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_CalculateHashCode_SameNameDifDepsGivesDifHashcode()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            var dependency1 = new DebugNameTestOperation("Dependency 1");
            var dependency2 = new DebugNameTestOperation("Dependency 2");

            var depList1 = new List<AsyncOperationHandle> { new AsyncOperationHandle(dependency1) };
            var depList2 = new List<AsyncOperationHandle> { new AsyncOperationHandle(dependency2) };

            DebugNameTestOperation op1 = new DebugNameTestOperation("Same name", depList1);
            AsyncOperationHandle handle1 = new AsyncOperationHandle(op1);

            DebugNameTestOperation op2 = new DebugNameTestOperation("Same name", depList2);
            AsyncOperationHandle handle2 = new AsyncOperationHandle(op2);

            Assert.AreNotEqual(rmd.CalculateHashCode(handle1), rmd.CalculateHashCode(handle2), "Two separate handles with the same DebugName, but different dependency names should not have the same hashcode.");
        }

        [UnityTest]
        public IEnumerator ResourceManagerDiagnostics_CalculateHashCode_SameNameSameDepNamesGivesSameHashcode()
        {
            yield return Init();
            var rmd = new ResourceManagerDiagnostics(m_Addressables.ResourceManager);

            var dependency1 = new DebugNameTestOperation("Dependency 1");
            var dependency2 = new DebugNameTestOperation("Dependency 2");

            var depList1 = new List<AsyncOperationHandle> { new AsyncOperationHandle(dependency1), new AsyncOperationHandle(dependency2) };
            var depList2 = new List<AsyncOperationHandle> { new AsyncOperationHandle(dependency2), new AsyncOperationHandle(dependency1) };

            DebugNameTestOperation op1 = new DebugNameTestOperation("Same name", depList1);
            AsyncOperationHandle handle1 = new AsyncOperationHandle(op1);

            DebugNameTestOperation op2 = new DebugNameTestOperation("Same name", depList2);
            AsyncOperationHandle handle2 = new AsyncOperationHandle(op2);

            Assert.AreEqual(rmd.CalculateHashCode(handle1), rmd.CalculateHashCode(handle2), "Two handles with the same DebugName and same dependency names should have the same hashcode.");
        }

        [UnityTest]
        public IEnumerator PercentComplete_CalculationIsCorrect_WhenInAChainOperation()
        {
            yield return Init();

            float handle1PercentComplete = 0.6f;
            float handle2PercentComplete = 0.98f;

            AsyncOperationHandle<GameObject> slowHandle1 = new ManualPercentCompleteOperation(handle1PercentComplete, new DownloadStatus(){DownloadedBytes = 1, IsDone = false, TotalBytes = 2}).Handle;
            AsyncOperationHandle<GameObject> slowHandle2 = new ManualPercentCompleteOperation(handle2PercentComplete, new DownloadStatus() { DownloadedBytes = 1, IsDone = false, TotalBytes = 2 }).Handle;

            slowHandle1.m_InternalOp.m_RM = m_Addressables.ResourceManager;
            slowHandle2.m_InternalOp.m_RM = m_Addressables.ResourceManager;

            var chainOperation = m_Addressables.ResourceManager.CreateChainOperation(slowHandle1, (op) =>
            {
                return slowHandle2;
            });

            chainOperation.m_InternalOp.Start(m_Addressables.ResourceManager, default, null);

            Assert.AreEqual((handle1PercentComplete + handle2PercentComplete) / 2, chainOperation.PercentComplete);
        }

        [UnityTest]
        public IEnumerator RuntimeKeyIsValid_ReturnsFalseForInValidKeys()
        {
            yield return Init();

            AsyncOperationHandle handle = m_Addressables.InstantiateAsync(AssetReferenceObjectKey);
            yield return handle;
            Assert.IsNotNull(handle.Result as GameObject);
            AssetReferenceTestBehavior behavior =
                (handle.Result as GameObject).GetComponent<AssetReferenceTestBehavior>();

            Assert.IsFalse((behavior.InValidAssetReference as IKeyEvaluator).RuntimeKeyIsValid());
            Assert.IsFalse((behavior.InvalidLabelReference as IKeyEvaluator).RuntimeKeyIsValid());

            handle.Release();
        }

        static ResourceLocationMap GetRLM(AddressablesImpl addr)
        {
            foreach (var rl in addr.m_ResourceLocators)
            {
                if (rl.Locator is ResourceLocationMap)
                    return rl.Locator as ResourceLocationMap;
            }
            return null;
        }

        private void SetupBundleForCacheDependencyClearTests(string bundleName, string depName, string hash, string key, out ResourceLocationBase location)
        {
            CreateFakeCachedBundle(bundleName, hash);
            location = new ResourceLocationBase(key, bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource),
                new ResourceLocationBase(depName, bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource)));
            (location.Dependencies[0] as ResourceLocationBase).Data = location.Data = new AssetBundleRequestOptions()
            {
                BundleName = bundleName
            };
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForKey()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING

            string hash = "123456789";
            string bundleName = $"test_{hash}";
            string key = "lockey_key";

            List<Hash128> versions = new List<Hash128>();
            ResourceLocationBase location = null;
            SetupBundleForCacheDependencyClearTests(bundleName, "bundle", hash, key, out location);

            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(1, versions.Count);
            versions.Clear();
            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() { location });

            yield return m_Addressables.ClearDependencyCacheAsync((object)key, true);
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForKeyWithDependencies()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING

            string hash = "123456789";
            string bundleName = $"test_{hash}";

            string depHash = "97564231";
            string depBundleName = $"test_{depHash}";
            ResourceLocationBase depLocation = null;

            string key = "lockey_deps_key";

            SetupBundleForCacheDependencyClearTests(depBundleName, "test", depHash, "depKey", out depLocation);
            CreateFakeCachedBundle(bundleName, hash);

            ResourceLocationBase location = new ResourceLocationBase(key, bundleName,
                typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource),
                new ResourceLocationBase("bundle", bundleName, typeof(AssetBundleProvider).FullName,
                    typeof(IAssetBundleResource)),
                depLocation);
            (location.Dependencies[0] as ResourceLocationBase).Data = location.Data = new AssetBundleRequestOptions()
            {
                BundleName = bundleName
            };

            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() {location});

            yield return m_Addressables.ClearDependencyCacheAsync((object)key, true);

            List<Hash128> versions = new List<Hash128>();
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
            versions.Clear();
            Caching.GetCachedVersions(depBundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForLocationWithDependencies()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING
            string hash = "123456789";
            string bundleName = $"test_{hash}";
            string key = "lockey_deps_location";

            string depHash = "97564231";
            string depBundleName = $"test_{depHash}";
            ResourceLocationBase depLocation = null;

            SetupBundleForCacheDependencyClearTests(depBundleName, "test", depHash, "depKey", out depLocation);
            CreateFakeCachedBundle(bundleName, hash);

            ResourceLocationBase location = new ResourceLocationBase(key, bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource),
                new ResourceLocationBase("bundle", bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource)),
                depLocation);
            (location.Dependencies[0] as ResourceLocationBase).Data = location.Data = new AssetBundleRequestOptions()
            {
                BundleName = bundleName
            };

            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() { location });

            yield return m_Addressables.ClearDependencyCacheAsync(location, true);

            List<Hash128> versions = new List<Hash128>();
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
            versions.Clear();
            Caching.GetCachedVersions(depBundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForLocationList()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING
            string hash = "123456789";
            string bundleName = $"test_{hash}";
            string key = "lockey_location_list";
            ResourceLocationBase location = null;
            SetupBundleForCacheDependencyClearTests(bundleName, "bundle", hash, key, out location);

            List<Hash128> versions = new List<Hash128>();
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(1, versions.Count);
            versions.Clear();

            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() { location });

            yield return m_Addressables.ClearDependencyCacheAsync(new List<IResourceLocation>() { location }, true);
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForKeyList()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING

            string hash = "123456789";
            string bundleName = $"test_{hash}";
            string key = "lockey_key_list";
            ResourceLocationBase location = null;

            SetupBundleForCacheDependencyClearTests(bundleName, "bundle", hash, key, out location);

            List<Hash128> versions = new List<Hash128>();
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(1, versions.Count);
            versions.Clear();

            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() { location });

            yield return m_Addressables.ClearDependencyCacheAsync(new List<object>() { (object)key }, true);
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForLocationListWithDependencies()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING

            string hash = "123456789";
            string bundleName = $"test_{hash}";
            string key = "lockey_deps_location_list";

            string depHash = "97564231";
            string depBundleName = $"test_{depHash}";
            ResourceLocationBase depLocation = null;

            SetupBundleForCacheDependencyClearTests(depBundleName, "test", depHash, "depKey", out depLocation);

            CreateFakeCachedBundle(bundleName, hash);
            ResourceLocationBase location = new ResourceLocationBase(key, bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource),
                new ResourceLocationBase("bundle", bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource)),
                depLocation);
            (location.Dependencies[0] as ResourceLocationBase).Data = location.Data = new AssetBundleRequestOptions()
            {
                BundleName = bundleName
            };

            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() { location });

            yield return m_Addressables.ClearDependencyCacheAsync(new List<IResourceLocation>() { location }, true);

            List<Hash128> versions = new List<Hash128>();
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
            versions.Clear();
            Caching.GetCachedVersions(depBundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ClearDependencyCache_ClearsAllCachedFilesForKeyListWithDependencies()
        {
            yield return Init();
            var rlm = GetRLM(m_Addressables);
            if (rlm == null)
                yield break;
#if ENABLE_CACHING

            string hash = "123456789";
            string bundleName = $"test_{hash}";
            string key = "lockey_deps_key_list";

            string depHash = "97564231";
            string depBundleName = $"test_{depHash}";
            ResourceLocationBase depLocation = null;

            SetupBundleForCacheDependencyClearTests(depBundleName, "test", depHash, "depKey", out depLocation);

            CreateFakeCachedBundle(bundleName, hash);
            ResourceLocationBase location = new ResourceLocationBase(key, bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource),
                new ResourceLocationBase("bundle", bundleName, typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource)),
                depLocation);
            (location.Dependencies[0] as ResourceLocationBase).Data = location.Data = new AssetBundleRequestOptions()
            {
                BundleName = bundleName
            };

            rlm.Add(location.PrimaryKey, new List<IResourceLocation>() { location });


            yield return m_Addressables.ClearDependencyCacheAsync(new List<object>() { key }, true);

            List<Hash128> versions = new List<Hash128>();
            Caching.GetCachedVersions(bundleName, versions);
            Assert.AreEqual(0, versions.Count);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator AssetBundleRequestOptions_ComputesCorrectSize_WhenLocationDoesNotMatchBundleName_WithoutHash()
        {
#if ENABLE_CACHING
            yield return Init();

            //Setup
            string bundleName = "bundle";
            string bundleLocation = "http://fake.com/default-bundle";
            long size = 123;
            IResourceLocation location = new ResourceLocationBase("test", bundleLocation,
                typeof(AssetBundleProvider).FullName, typeof(GameObject));

            Hash128 hash = Hash128.Compute("213412341242134");
            AssetBundleRequestOptions abro = new AssetBundleRequestOptions()
            {
                BundleName = bundleName,
                BundleSize = size,
            };

            //Test
            Assert.AreEqual(size, abro.ComputeSize(location, m_Addressables.ResourceManager));
            CreateFakeCachedBundle(bundleName, hash.ToString());
            Assert.AreEqual(0, abro.ComputeSize(location, m_Addressables.ResourceManager));

            //Cleanup
            Caching.ClearAllCachedVersions(bundleName);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator AssetBundleRequestOptions_ComputesCorrectSize_WhenLocationDoesNotMatchBundleName_WithHash()
        {
#if ENABLE_CACHING
            yield return Init();

            //Setup
            string bundleName = "bundle";
            string bundleLocation = "http://fake.com/default-bundle";
            long size = 123;
            IResourceLocation location = new ResourceLocationBase("test", bundleLocation,
                typeof(AssetBundleProvider).FullName, typeof(GameObject));

            Hash128 hash = Hash128.Compute("213412341242134");
            AssetBundleRequestOptions abro = new AssetBundleRequestOptions()
            {
                BundleName = bundleName,
                BundleSize = size,
                Hash = hash.ToString()
            };

            //Test
            Assert.AreEqual(size, abro.ComputeSize(location, m_Addressables.ResourceManager));
            CreateFakeCachedBundle(bundleName, hash.ToString());
            Assert.AreEqual(0, abro.ComputeSize(location, m_Addressables.ResourceManager));

            //Cleanup
            Caching.ClearAllCachedVersions(bundleName);
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator AssetBundleResource_RemovesCachedBundle_OnLoadFailure()
        {
#if ENABLE_CACHING
            yield return Init();
            string bundleName = "bundleName";
            Hash128 hash = Hash128.Parse("123456");
            uint crc = 1;
            AssetBundleResource abr = new AssetBundleResource();
            abr.m_ProvideHandle = new ProvideHandle(m_Addressables.ResourceManager, new ProviderOperation<AssetBundleResource>());
            abr.m_Options = new AssetBundleRequestOptions()
            {
                BundleName = bundleName,
                Hash = hash.ToString(),
                Crc = crc,
                RetryCount = 3
            };
            CreateFakeCachedBundle(bundleName, hash.ToString());
            CachedAssetBundle cab = new CachedAssetBundle(bundleName, hash);
            var request = abr.CreateWebRequest(new ResourceLocationBase("testName", bundleName, typeof(AssetBundleProvider).FullName,
                typeof(IAssetBundleResource)));

            Assert.IsTrue(Caching.IsVersionCached(cab));
            yield return request.SendWebRequest();
            Assert.IsFalse(Caching.IsVersionCached(cab));
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator AssetBundleResource_RemovesCachedBundle_OnLoadFailure_WhenRetryCountIsZero()
        {
#if ENABLE_CACHING
            yield return Init();
            string bundleName = "bundleName";
            Hash128 hash = Hash128.Parse("123456");
            uint crc = 1;
            AssetBundleResource abr = new AssetBundleResource();
            abr.m_ProvideHandle = new ProvideHandle(m_Addressables.ResourceManager, new ProviderOperation<AssetBundleResource>());
            abr.m_Options = new AssetBundleRequestOptions()
            {
                BundleName = bundleName,
                Hash = hash.ToString(),
                Crc = crc,
                RetryCount = 0
            };
            CreateFakeCachedBundle(bundleName, hash.ToString());
            CachedAssetBundle cab = new CachedAssetBundle(bundleName, hash);
            var request = abr.CreateWebRequest(new ResourceLocationBase("testName", bundleName, typeof(AssetBundleProvider).FullName,
                typeof(IAssetBundleResource)));

            Assert.IsTrue(Caching.IsVersionCached(cab));
            yield return request.SendWebRequest();
            Assert.IsFalse(Caching.IsVersionCached(cab));
#else
            Assert.Ignore("Caching not enabled.");
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator WebRequestQueue_GetsSetOnInitialization_FromRuntimeData()
        {
            yield return Init();
            Assert.AreEqual(AddressablesTestUtility.kMaxWebRequestCount, WebRequestQueue.s_MaxRequest);
        }

        string CreateFakeCachedBundle(string bundleName, string hash)
        {
#if ENABLE_CACHING
            string fakeCachePath = string.Format("{0}/{1}/{2}", Caching.currentCacheForWriting.path, bundleName, hash);
            Directory.CreateDirectory(fakeCachePath);
            var dataFile = File.Create(Path.Combine(fakeCachePath, "__data"));
            var infoFile = File.Create(Path.Combine(fakeCachePath, "__info"));

            byte[] info = new UTF8Encoding(true).GetBytes(
@"-1
1554740658
1
__data");
            infoFile.Write(info, 0, info.Length);

            dataFile.Dispose();
            infoFile.Dispose();

            return fakeCachePath;
#else
            return null;
#endif
        }
    }
}
