using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 这个输入系统只能通过InputActionAsset绑定
/// </summary>
public class UseExample : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActionAsset; // 使用与 InputMgr 相同的 asset
    
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction fireAction;
    private InputAction lookAction;
    
    private void Start()
    {
        // 优先使用序列化的 asset，如果没指定则从 InputMgr 获取
        // 这样就能确保使用与 InputMgr 相同的实例，改键会立即生效
        if (inputActionAsset == null)
        {
            inputActionAsset = InputBindingMgr.Instance.GetInputActionAsset();
            if (inputActionAsset == null)
            {
                Debug.LogError("TestMono: 无法获取 InputActionAsset！请确保 InputMgr 已初始化");
                return;
            }
        }
        
        // 直接从 asset 获取 ActionMap 和 Actions
        playerMap = inputActionAsset.FindActionMap("Player");
        if (playerMap != null)
        {
            playerMap.Enable();
            
            moveAction = playerMap.FindAction("Move");
            fireAction = playerMap.FindAction("Fire");
            lookAction = playerMap.FindAction("Look");
            
            
            if (moveAction != null)
            {
                moveAction.performed += (context) => { print("Move"+context.ReadValue<Vector2>()); };
            }
            
            if (fireAction != null)
            {
                fireAction.performed += (context) => { print("Fire"); };
            }
        }
        else
        {
            Debug.LogError("TestMono: 找不到 Player ActionMap");
        }
    }
    
    private void OnDisable()
    {
        // 禁用 ActionMap（禁用后事件不会再触发）
        if (playerMap != null)
        {
            playerMap.Disable();
        }
    }    
}