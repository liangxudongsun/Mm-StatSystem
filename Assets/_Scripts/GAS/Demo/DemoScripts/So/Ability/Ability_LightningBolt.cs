using Cysharp.Threading.Tasks;
using GAS.Component;
using GAS.Core;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using GAS.TaskSystem;
using GAS.Targeting;
using UnityEngine;

namespace GAS.AbilitySystem
{
    /// <summary>
    /// 雷电球技能 - 创建后0.5s飞向敌人，遇到敌人销毁并造成伤害
    /// </summary>
    [CreateAssetMenu(fileName = "Ability_LightningBolt", menuName = "GAS/Demo/Ability_LightningBolt")]
    public class Ability_LightningBolt : GameplayAbility
    {
        [Header("雷电球设置")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] public float projectileSpeed = 15f;
        [SerializeField] public float delayBeforeLaunch = 0.5f;
        [SerializeField] public float arriveDistance = 1f;

        [Header("伤害设置")]
        [SerializeField] private GameplayEffectData damageEffect;

        public override void Activate(AbilityContext context)
        {
            Debug.Log($"[雷电球] 激活技能");

            // 获取目标
            var player = context.StatController.GetComponent<Demo_Player>();
            if (player == null)
            {
                Debug.LogWarning("[雷电球] 未找到Demo_Player组件!");
                return;
            }

            // 获取ASC
            var asc = context.Owner;
            if (asc == null)
            {
                Debug.LogWarning("[雷电球] 未找到AbilitySystemComponent!");
                return;
            }

            Transform target = player.GetCurrentTarget();
            AbilitySystemComponent targetASC = null;

            if (target != null)
            {
                targetASC = target.GetComponent<AbilitySystemComponent>();
                if (!TargetingSystem.IsValidTarget(asc, targetASC, true))
                {
                    target = null;
                    targetASC = null;
                }
            }

            if (target == null)
            {
                var targetData = TargetingSystem.GetTargetData(
                    asc,
                    TargetType.SingleEnemy,
                    asc.transform.position,
                    asc.transform.forward,
                    30f);

                targetASC = targetData.SingleTarget;
                target = targetASC != null ? targetASC.transform : null;
            }

            if (target == null || targetASC == null)
            {
                Debug.LogWarning("[雷电球] 未选中目标!");
                return;
            }

            // 启动异步任务
            ExecuteLightningBolt(context, target, targetASC).Forget();
        }

        private async UniTask ExecuteLightningBolt(AbilityContext context, Transform target, AbilitySystemComponent targetASC)
        {
            // 1. 在玩家前方生成投射物
            var asc = context.Owner;
            var ownerTransform = asc.transform;
            // 计算生成位置：玩家位置 + 玩家朝向 * 1.5f（前方一点）
            Vector3 spawnPosition = ownerTransform.position + ownerTransform.forward * 1.5f;
            spawnPosition.y += 1f; // 稍微抬高一点
            
            GameObject projectile = null;

            var spawnTask = context.RegisterTask(Task_SpawnEffect.SpawnEffect(asc)
                .SetEffectPrefab(projectilePrefab)
                .SetLocation(spawnPosition)
                .SetDuration(-1)); // 不自动销毁
            spawnTask.Start();
            await UniTask.WaitUntil(() => spawnTask.IsEnded, cancellationToken: spawnTask.CancellationToken);
            projectile = spawnTask.SpawnedEffect;

            // 设置投射物朝向目标方向
            if (projectile != null && target != null)
            {
                projectile.transform.LookAt(target);
            }

            if (projectile == null || target == null)
            {
                return;
            }

            // 2. 等待0.5秒
            await UniTask.Delay((int)(delayBeforeLaunch * 1000), cancellationToken: context.CancellationToken);

            if (projectile == null || target == null)
            {
                return;
            }

            // 3. 飞向目标
            var moveTask = context.RegisterTask(Task_MoveToTarget.MoveToTarget(asc)
                .SetTarget(target)
                .SetProjectile(projectile)
                .SetSpeed(projectileSpeed)
                .SetArriveDistance(arriveDistance)
                .SetDestroyOnArrive(false));
            moveTask.Start();
            await UniTask.WaitUntil(() => moveTask.IsEnded, cancellationToken: context.CancellationToken);

            // 4. 命中目标
            if (projectile != null && target != null)
            {
                // 应用伤害GE
                if (targetASC != null && damageEffect != null)
                {
                    targetASC.ApplyGE(damageEffect, asc.transform);
                }

                // 生成命中特效
                var hitTask = context.RegisterTask(Task_SpawnEffect.SpawnEffect(asc)
                    .SetEffectPrefab(hitEffectPrefab)
                    .SetTarget(target)
                    .SetDuration(0.5f));
                hitTask.Start();

                // 销毁投射物
                Destroy(projectile);
            }
        }
    }
}
