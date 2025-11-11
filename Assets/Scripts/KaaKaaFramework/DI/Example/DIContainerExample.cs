using UnityEngine;

namespace KaaKaaFramework.DI.Example
{
    /// <summary>
    /// DI容器使用示例
    /// 演示三种注册方式：直接注册实例、注册接口映射、自动依赖注入
    /// </summary>
    public class DIContainerExample : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("=== DI容器使用示例 ===");
            
            // 示例1: 直接注册实例（单例模式）
            Example1_RegisterInstance();
            
            // 示例2: 注册接口映射（延迟创建）
            Example2_RegisterInterface();
            
            // 示例3: 自动依赖注入（构造函数注入）
            Example3_AutoDependencyInjection();
        }

        /// <summary>
        /// 示例1: 直接注册实例
        /// 适用于已经创建好的对象，直接注册到容器中
        /// </summary>
        void Example1_RegisterInstance()
        {
            Debug.Log("\n--- 示例1: 直接注册实例 ---");
            
            // 创建配置对象
            var gameConfig = new GameConfig
            {
                PlayerSpeed = 10f,
                MaxHealth = 100
            };
            
            // 注册到DI容器
            DIContainer.Instance.Register(gameConfig);
            
            // 使用时解析
            var config = DIContainer.Instance.Resolve<GameConfig>();
            Debug.Log($"玩家速度: {config.PlayerSpeed}, 最大生命值: {config.MaxHealth}");
        }

        /// <summary>
        /// 示例2: 注册接口映射
        /// 适用于接口和实现类，容器会在需要时自动创建实例
        /// 注意：对于接口映射，需要使用 Resolve(Type) 方法，而不是 Resolve&lt;T&gt;()
        /// </summary>
        void Example2_RegisterInterface()
        {
            Debug.Log("\n--- 示例2: 注册接口映射 ---");
            
            // 注册接口到实现的映射
            DIContainer.Instance.Register<IDataService, FileDataService>();
            
            // 使用时解析（会自动创建FileDataService实例）
            // 注意：对于接口映射，需要使用 Resolve(Type) 方法
            var dataService = DIContainer.Instance.Resolve(typeof(IDataService)) as IDataService;
            dataService.SaveData("测试数据");
            dataService.LoadData();
            
            // 第一次解析后，实例会被缓存，之后可以使用 Resolve<T>()
            var dataService2 = DIContainer.Instance.Resolve<IDataService>();
            dataService2.SaveData("第二次调用");
        }

        /// <summary>
        /// 示例3: 自动依赖注入
        /// 容器会自动解析构造函数中的依赖并注入
        /// </summary>
        void Example3_AutoDependencyInjection()
        {
            Debug.Log("\n--- 示例3: 自动依赖注入 ---");
            
            // 1. 先注册依赖服务（接口映射）
            DIContainer.Instance.Register<ILogger, UnityLogger>();
            DIContainer.Instance.Register<ISaveService, FileSaveService>();
            
            // 2. 注册需要依赖注入的服务
            // PlayerController的构造函数需要ILogger和ISaveService
            // 容器会自动创建并注入这些依赖
            DIContainer.Instance.Register<IPlayerController, PlayerController>();
            
            // 3. 解析使用（容器会自动创建所有依赖）
            // 注意：对于接口映射，第一次需要使用 Resolve(Type) 方法
            var player = DIContainer.Instance.Resolve(typeof(IPlayerController)) as IPlayerController;
            player.Move();
            player.Attack();
        }
    }

    // ========== 示例1: 配置类 ==========
    public class GameConfig
    {
        public float PlayerSpeed { get; set; }
        public int MaxHealth { get; set; }

        public float Volume {  get; set; }
    }

    // ========== 示例2: 数据服务接口和实现 ==========
    public interface IDataService
    {
        void SaveData(string data);
        void LoadData();
    }

    public class FileDataService : IDataService
    {
        public void SaveData(string data)
        {
            Debug.Log($"[FileDataService] 保存数据到文件: {data}");
        }

        public void LoadData()
        {
            Debug.Log("[FileDataService] 从文件加载数据");
        }
    }

    // ========== 示例3: 依赖注入示例 ==========
    public interface ILogger
    {
        void Log(string message);
    }

    public class UnityLogger : ILogger
    {
        public void Log(string message)
        {
            Debug.Log($"[Logger] {message}");
        }
    }

    public interface ISaveService
    {
        void Save();
    }

    public class FileSaveService : ISaveService
    {
        public void Save()
        {
            Debug.Log("[SaveService] 保存游戏数据到文件");
        }
    }

    public interface IPlayerController
    {
        void Move();
        void Attack();
    }

    public class PlayerController : IPlayerController
    {
        private ILogger _logger;
        private ISaveService _saveService;

        // 构造函数需要ILogger和ISaveService
        // DI容器会自动解析并注入这些依赖
        public PlayerController(ILogger logger, ISaveService saveService)
        {
            _logger = logger;
            _saveService = saveService;
            _logger.Log("PlayerController 初始化完成");
        }

        public void Move()
        {
            _logger.Log("玩家移动");
            _saveService.Save();
        }

        public void Attack()
        {
            _logger.Log("玩家攻击");
        }
    }
}

