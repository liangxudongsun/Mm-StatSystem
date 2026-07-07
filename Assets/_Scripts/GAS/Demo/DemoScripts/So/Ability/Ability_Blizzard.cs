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
    /// 暴风雪技能 - 大范围持续伤害，跟随玩家移动
    /// </summary>
    [CreateAssetMenu(fileName = "Ability_Blizzard", menuName = "GAS/Demo/Ability_Blizzard")]
    public class Ability_Blizzard : GameplayAbility
    {
        [Header("暴风雪设置")]
        [SerializeField] private GameObject blizzardEffectPrefab;
        [SerializeField] public float blizzardRadius = 5f;
        [SerializeField] public LayerMask enemyLayer;

        [Header("伤害设置")]
        [SerializeField] private GameplayEffectData blizzardDamageEffect;
        [SerializeField] public float damageInterval = 0.5f;

        public override void Activate(AbilityContext context)
        {
            Debug.Log($"[暴风雪] 激活技能");

            var player = context.StatController.GetComponent<Demo_Player>();
            if (player == null)
            {
                Debug.LogWarning("[暴风雪] 未找到Demo_Player组件!");
                return;
            }

            var asc = context.Owner;
            if (asc == null)
            {
                Debug.LogWarning("[暴风雪] 未找到AbilitySystemComponent!");
                return;
            }

            ExecuteBlizzard(context, player).Forget();
        }

        private async UniTask ExecuteBlizzard(AbilityContext context, Demo_Player player)
        {
            var asc = context.Owner;
            var duration = blizzardDamageEffect.DurationValue;
            var elapsed = 0f;
            GameObject effect = null;

            // 1. 生成暴风雪特效 (不设置duration，让技能自己管理生命周期)
            Debug.Log($"[暴风雪] 开始生成特效: prefab={blizzardEffectPrefab}, duration={duration}");
            var spawnTask = context.RegisterTask(Task_SpawnEffect.SpawnEffect(asc)
                .SetEffectPrefab(blizzardEffectPrefab)
                .SetLocation(player.GetPosition()));
                //.SetDuration(duration); // 不设置duration，避免特效被提前销毁
            spawnTask.Start();
            await UniTask.WaitUntil(() => spawnTask.IsEnded, cancellationToken: spawnTask.CancellationToken);
            effect = spawnTask.SpawnedEffect;

            Debug.Log($"[暴风雪] 特效生成结果: effect={effect}");
            if (effect == null)
            {
                Debug.LogWarning("[暴风雪] 特效为null，无法继续！");
                return;
            }

            // 2. 启动跟随任务
            var followTask = context.RegisterTask(Task_UpdatePosition.UpdatePosition(asc)
                .SetTarget(player.transform)
                .SetEffect(effect)
                .SetKeepYOffset(true));
            followTask.Start();

            // 3. 周期性造成伤害
            var timer = 0f;
            Debug.Log($"[暴风雪] 开始伤害循环: duration={duration}");
            while (elapsed < duration && !context.IsCancelled && effect != null)
            {
                await UniTask.Yield();
                if (effect == null)
                {
                    Debug.LogWarning("[暴风雪] effect变为null，退出循环");
                    break;
                }

                elapsed += Time.deltaTime;
                timer += Time.deltaTime;
                Debug.Log($"[暴风雪] 循环中: elapsed={elapsed}, timer={timer}");

                if (timer >= damageInterval)
                {
                    timer = 0f;
                    ApplyBlizzardDamage(asc, player.transform.position);
                }
            }
            Debug.Log($"[暴风雪] 循环结束: elapsed={elapsed}, duration={duration}");

            // 清理
            if (effect != null)
            {
                Destroy(effect);
            }

            Debug.Log("[暴风雪] 技能结束");
        }

        private void ApplyBlizzardDamage(AbilitySystemComponent asc, Vector3 center)
        {
            Debug.Log($"[暴风雪] 开始检测敌人: center={center}, radius={blizzardRadius}, enemyLayer={enemyLayer.value}");
            var targetData = TargetingSystem.GetTargetData(
                asc,
                TargetType.AreaEnemy,
                center,
                asc.transform.forward,
                blizzardRadius,
                layerMask: enemyLayer);

            Debug.Log($"[暴风雪] 找到 {targetData.TargetList.Count} 个目标");

            foreach (var enemyASC in targetData.TargetList)
            {
                if (enemyASC != null && blizzardDamageEffect != null)
                {
                    Debug.Log($"[暴风雪] 应用GE到 {enemyASC.name}");
                    enemyASC.ApplyGE(blizzardDamageEffect, asc.transform);
                }
            }

            Debug.Log($"[暴风雪] 命中 {targetData.TargetList.Count} 个敌人");
        }
    }
}
