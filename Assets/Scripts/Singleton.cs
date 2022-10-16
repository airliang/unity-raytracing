using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 参考wiki http://wiki.unity3d.com/index.php/Toolbox
/// Be aware this will not prevent a non singleton constructor
///   such as `T myT = new T();`
/// To prevent that, add `protected T () {}` to your singleton class.
/// 
/// As a note, this is made as MonoBehaviour because we need Coroutines.
/// </summary>
public abstract class MonoBehaviorSingleton<T> : MonoBehaviour where T : MonoBehaviorSingleton<T>
{
    protected static T _instance;
    private static readonly object _lock = new object();
    private static bool s_applicationIsQuitting;
    

    public static T CreateComp()
    {
        T comp;
        Object[] moduleMgrs = FindObjectsOfType(typeof(T));
        if (moduleMgrs.Length > 1)
        {
            comp = (T)moduleMgrs[0];
            Debug.LogError("[Singleton] Something went really wrong " +
                " - there should never be more than 1 singleton!" +
                " Reopening the scene might fix it.");
            return comp;
        }

        if (moduleMgrs.Length == 0)
        {
            GameObject singleton = new GameObject();
            comp = singleton.AddComponent<T>();
            singleton.name = "(singleton) " + typeof(T);

            DontDestroyOnLoad(singleton);

            Debug.Log("[Singleton] An instance of " + typeof(T) +
                " is needed in the scene, so '" + singleton +
                "' was created with DontDestroyOnLoad.");
        }
        else
        {
            comp = (T)moduleMgrs[0];
            Debug.Log("[Singleton] Using instance Already created: " +
                _instance.gameObject.name);
        }

        return comp;
    }

    public static T Get()
    {
        return Instance;
    }

    public static T Create()
    {
        return (_instance = CreateComp());
    }
    public static T Instance
    {
        get
        {
            if (s_applicationIsQuitting)
            {
                //Debug.LogWarning("[Singleton] Instance '" + typeof(T) +
                //    "' already destroyed on application quit." +
                //    " Won't create again - returning null.");
                return default(T);
            }

            return _instance;
        }
    }

    public static bool HasInstance
    {
        get
        {
            return _instance != null;
        }
    }

    public virtual void OnInitialize()
    {
        
    }
    /// <summary>
    /// MonoBehavior的单例由OnDestroy
    /// </summary>
    public virtual void OnFinalize() { }

    protected void Awake()
    {
        Initialize(this as T);
    }

    static void Initialize(T instance)
    {
        if (_instance == null)
        {
            _instance = instance;

            _instance.OnInitialize();
        }
        else if (_instance != instance)
        {
            DestroyImmediate(instance.gameObject);
        }
    }

    void OnDestroy()
    {
        Destroyed(this as T);
    }

    /// <summary>
    /// When Unity quits, it destroys objects in a random order.
    /// In principle, a Singleton is only destroyed when application quits.
    /// If any script calls Instance after it have been destroyed, 
    ///   it will create a buggy ghost object that will stay on the Editor scene
    ///   even after stopping playing the Application. Really bad!
    /// So, this was made to be sure we're not creating that buggy ghost object.
    /// </summary>
    void OnApplicationQuit()
    {
        s_applicationIsQuitting = true;
        Destroyed(this as T);
    }

    static void Destroyed(T instance)
    {
        if (_instance == instance)
        {
            _instance.OnFinalize();
            _instance = null;
        }
    }
}

/// <summary>
/// 用于静态单件，方便调用
/// 如果要测试组件的话，直接继承MonoBehaviorSingleton,可以利用MonoBehavior的一些属性
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class Singleton<T>  where T : new ()
{
    protected static T _instance;

    private static readonly object Lock = new object();

    public static T Instance
    {
        get
        {
            lock (Lock)
            {
                if (_instance != null) return _instance;
                _instance = new T();

                return _instance;
            }
        }
    }
}
