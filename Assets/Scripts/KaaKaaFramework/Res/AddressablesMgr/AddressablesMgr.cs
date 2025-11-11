using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesMgr : BaseManager<AddressablesMgr>
{
    private AddressablesMgr() {  }

    //有一个容器 帮助我们存储 异步加载的返回值
    public Dictionary<string, IEnumerator> resDic = new Dictionary<string, IEnumerator>();

    //用协程来异步加载资源的方法
    public void LoadAssetCoroutine<T>(string name, Action<AsyncOperationHandle<T>> callback)
    {
        MonoMgr.Instance.StartGlobalCoroutine(LoadAssetAsyncInternal(name, callback));
    }

    private IEnumerator LoadAssetAsyncInternal<T>(string name, Action<AsyncOperationHandle<T>> callback)
    {
        string keyName = name + "_" + typeof(T).Name;
        AsyncOperationHandle<T> handle;

        if (resDic.ContainsKey(keyName))
        {
            handle = (AsyncOperationHandle<T>)resDic[keyName];
            yield return handle;
            callback(handle);
        }
        else
        {
            handle = Addressables.LoadAssetAsync<T>(name);
            resDic.Add(keyName, handle);
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
                callback(handle);
            else
            {
                Debug.LogWarning(keyName + " 资源加载失败");
                if (resDic.ContainsKey(keyName))
                    resDic.Remove(keyName);
            }
        }
    }

    /// <summary>
    /// 每次load完一个资源就回调一次
    /// 确保name是唯一的
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mode"></param>
    /// <param name="callBack"></param>
    /// <param name="keys"></param>
    public void LoadAssetsCoroutinePer<T>( Action<T> callBack, params string[] keys)
    {
        MonoMgr.Instance.StartGlobalCoroutine(LoadAssetsAsyncInternalPer( callBack, keys));
    }
    private IEnumerator LoadAssetsAsyncInternalPer<T>(Action<T> callBack, params string[] keys)
    {
        if (keys == null || keys.Length == 0)
        {
            Debug.LogWarning("未提供任何资源键。");
            yield break;
        }

        // 遍历每一个资源键，按顺序加载
        foreach (string key in keys)
        {
            string keyName = key + "_" + typeof(T).Name;
            AsyncOperationHandle<T> handle;

            // 检查缓存
            if (resDic.ContainsKey(keyName))
            {
                handle = (AsyncOperationHandle<T>)resDic[keyName];
            }
            else
            {
                // 发起新的加载请求
                handle = Addressables.LoadAssetAsync<T>(key);
                resDic.Add(keyName, handle);
            }

            // 等待单个资源加载完成
            yield return handle;

            // 加载完成后，检查状态并调用回调
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                callBack(handle.Result);
            }
            else
            {
                Debug.LogError($"资源 {keyName} 顺序加载失败！");
                if (resDic.ContainsKey(keyName))
                    resDic.Remove(keyName);

                // 加载失败时停止后续加载
                yield break;
            }
        }
    }

    /// <summary>
    /// 所有资源加载完毕后回调一次
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mode"></param>
    /// <param name="callBack"></param>
    /// <param name="keys"></param>
    public void LoadAssetsCoroutineUni<T>(Addressables.MergeMode mode, Action<IList<T>> callBack, params string[] keys)
    {
        MonoMgr.Instance.StartGlobalCoroutine(LoadAssetsAsyncInternalUni(mode, callBack, keys));
    }
    private IEnumerator LoadAssetsAsyncInternalUni<T>(Addressables.MergeMode mode, Action<IList<T>> callBack, params string[] keys)
    {
        List<string> list = new List<string>(keys);
        string keyName = "";
        foreach (string key in list)
            keyName += key + "_";
        keyName += typeof(T).Name;

        // 获取异步操作句柄
        AsyncOperationHandle<IList<T>> handle;

        // 检查资源是否已在缓存中
        if (resDic.ContainsKey(keyName))
        {
            // 如果已缓存，直接从字典中取出句柄
            handle = (AsyncOperationHandle<IList<T>>)resDic[keyName];
        }
        else
        {
            // 如果未缓存，发起新的异步加载请求，注意这里传入 null
            // 因为我们想在协程中手动处理回调
            handle = Addressables.LoadAssetsAsync<T>(list, null, mode);
            // 将句柄存入缓存字典
            resDic.Add(keyName, handle);
        }

        // 等待加载完成
        yield return handle;

        // 加载完成后，检查状态
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            // 只有当整个批量操作成功时，才调用回调
            // 传入整个资源列表
            callBack(handle.Result);
            Debug.Log($"资源组 {keyName} 加载成功！");
        }
        else
        {
            // 处理加载失败
            Debug.LogError($"资源组 {keyName} 加载失败！");
            // 失败后从字典中移除，避免下次加载时直接从失败的句柄中获取
            if (resDic.ContainsKey(keyName))
            {
                resDic.Remove(keyName);
            }
        }
    }

    //异步加载资源的方法
    public void LoadAssetAsync<T>(string name, Action<AsyncOperationHandle<T>> callBack)
    {
        //由于存在同名 不同类型资源的区分加载
        //所以我们通过名字和类型拼接作为 key
        string keyName = name + "_" + typeof(T).Name;
        AsyncOperationHandle<T> handle;
        //如果已经加载过该资源
        if (resDic.ContainsKey(keyName))
        {
            //获取异步加载返回的操作内容
            handle = (AsyncOperationHandle<T>)resDic[keyName];

            //判断 这个异步加载是否结束
            if (handle.IsDone)
            {
                //如果成功 就不需要异步了 直接相当于同步调用了 这个委托函数 传入对应的返回值
                callBack(handle);
            }
            //还没有加载完成
            else
            {
                //如果这个时候 还没有异步加载完成 那么我们只需要 告诉它 完成时做什么就行了
                handle.Completed += (obj) => {
                    if (obj.Status == AsyncOperationStatus.Succeeded)
                        callBack(obj);
                };
            }
            return;
        }

        //如果没有加载过该资源
        //直接进行异步加载 并且记录
        handle = Addressables.LoadAssetAsync<T>(name);
        handle.Completed += (obj) => {
            if (obj.Status == AsyncOperationStatus.Succeeded)
                callBack(obj);
            else
            {
                Debug.LogWarning(keyName + "资源加载失败");
                if (resDic.ContainsKey(keyName))
                    resDic.Remove(keyName);
            }
        };
        resDic.Add(keyName, handle);
    }

    

    //异步加载多个资源 或者 加载指定资源
    public void LoadAssetsAsync<T>(Addressables.MergeMode mode, Action<T> callBack, params string[] keys)
    {
        //1.构建一个keyName  之后用于存入到字典中
        List<string> list = new List<string>(keys);
        string keyName = "";
        foreach (string key in list)
            keyName += key + "_";
        keyName += typeof(T).Name;
        //2.判断是否存在已经加载过的内容 
        //存在做什么
        AsyncOperationHandle<IList<T>> handle;
        if (resDic.ContainsKey(keyName))
        {
            handle = (AsyncOperationHandle<IList<T>>)resDic[keyName];
            //异步加载是否结束
            if (handle.IsDone)
            {
                foreach (T item in handle.Result)
                    callBack(item);
            }
            else
            {
                handle.Completed += (obj) =>
                {
                    //加载成功才调用外部传入的委托函数
                    if (obj.Status == AsyncOperationStatus.Succeeded)
                    {
                        foreach (T item in handle.Result)
                            callBack(item);
                    }
                };
            }
            return;
        }
        //不存在做什么
        handle = Addressables.LoadAssetsAsync<T>(list, callBack, mode);
        handle.Completed += (obj) =>
        {
            if (obj.Status == AsyncOperationStatus.Failed)
            {
                Debug.LogError("资源加载失败" + keyName);
                if (resDic.ContainsKey(keyName))
                    resDic.Remove(keyName);
            }
        };
        resDic.Add(keyName, handle);
    }



    








    #region 同步加载资源
    /// <summary>
    /// 同步加载单个资源
    /// </summary>
    public T LoadAssetSync<T>(string name)
    {
        string keyName = name + "_" + typeof(T).Name;
        AsyncOperationHandle<T> handle;

        // 如果资源已经在异步加载中或已完成，直接等待结果
        if (resDic.ContainsKey(keyName))
        {
            handle = (AsyncOperationHandle<T>)resDic[keyName];
            handle.WaitForCompletion();
            return handle.Result;
        }
        else
        {
            // 如果资源还未加载，则进行同步加载
            handle = Addressables.LoadAssetAsync<T>(name);
            handle.WaitForCompletion();

            // 成功加载后存入字典
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                resDic.Add(keyName, handle);
                return handle.Result;
            }
            else
            {
                Debug.LogError($"同步加载资源失败: {name}");
                return default;
            }
        }
    }

    /// <summary>
    /// 同步加载多个资源
    /// </summary>
    public IList<T> LoadAssetsSync<T>(Addressables.MergeMode mode, params string[] keys)
    {
        List<string> list = new List<string>(keys);
        string keyName = "";
        foreach (string key in list)
            keyName += key + "_";
        keyName += typeof(T).Name;

        AsyncOperationHandle<IList<T>> handle;

        // 如果资源已经在异步加载中或已完成，直接等待结果
        if (resDic.ContainsKey(keyName))
        {
            handle = (AsyncOperationHandle<IList<T>>)resDic[keyName];
            handle.WaitForCompletion();
            return handle.Result;
        }
        else
        {
            // 如果资源还未加载，则进行同步加载
            handle = Addressables.LoadAssetsAsync<T>(list, null, mode);
            handle.WaitForCompletion();

            // 成功加载后存入字典
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                resDic.Add(keyName, handle);
                return handle.Result;
            }
            else
            {
                Debug.LogError($"同步加载多个资源失败: {keyName}");
                return null;
            }
        }
    }
    #endregion
    //释放资源的方法 
    public void Release<T>(string name)
    {
        //由于存在同名 不同类型资源的区分加载
        //所以我们通过名字和类型拼接作为 key
        string keyName = name + "_" + typeof(T).Name;
        if (resDic.ContainsKey(keyName))
        {
            //取出对象 移除资源 并且从字典里面移除
            AsyncOperationHandle<T> handle = (AsyncOperationHandle<T>)resDic[keyName];
            Addressables.Release(handle);
            resDic.Remove(keyName);
        }
    }
    // 释放资源的方法，用于多个资源
    public void Release<T>(params string[] keys)
    {
        //1.构建一个keyName  之后用于存入到字典中
        List<string> list = new List<string>(keys);
        string keyName = "";
        foreach (string key in list)
            keyName += key + "_";
        keyName += typeof(T).Name;

        if (resDic.ContainsKey(keyName))
        {
            //取出字典里面的对象
            AsyncOperationHandle<IList<T>> handle = (AsyncOperationHandle<IList<T>>)resDic[keyName];
            Addressables.Release(handle);
            resDic.Remove(keyName);
        }
    }



    //清空资源
    public void Clear()
    {
        resDic.Clear();
        AssetBundle.UnloadAllAssetBundles(true);
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }
}
