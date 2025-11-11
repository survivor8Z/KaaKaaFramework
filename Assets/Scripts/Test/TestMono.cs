using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMono : MonoBehaviour
{
    private void Awake()
    {
        
    }
    private void Start()
    {
        // 注册接口映射
        DIContainer.Instance.Register<ISaveLoad, SaveService>();
        DIContainer.Instance.Register<IAttack, Enemy>();
        
        IAttack enemy = DIContainer.Instance.Resolve(typeof(IAttack)) as IAttack;
        enemy.Attack();

    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.J))
        {
            IAttack enemy = DIContainer.Instance.Resolve(typeof(IAttack)) as IAttack;
            enemy.Attack();
        }
        
    }
}

public interface IAttack
{
    void Attack();
}

public interface ISaveLoad
{
    void Save();
    void Load();
}

public class SaveService:ISaveLoad
{
    public void Save()
    {
        Debug.Log("Save");
    }

    public void Load()
    {
        Debug.Log("Load");
    }
} 

public class Enemy : IAttack
{
    private ISaveLoad _saveLoad;
    private int AttackCount = 0;
    public Enemy(ISaveLoad saveLoad)
    {
        _saveLoad = saveLoad;
    }
    public void Attack()
    {
        _saveLoad.Load();
        Debug.Log("Attack"+AttackCount++);
        _saveLoad.Save();
    }
}
