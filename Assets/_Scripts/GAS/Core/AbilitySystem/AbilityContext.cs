using System.Collections.Generic;
using System.Threading;
using GAS.Component;
using GAS.StateSystem;
using GAS.TaskSystem;

namespace GAS.AbilitySystem
{
    /// <summary>
    /// 技能运行时上下文
    /// </summary>
    public class AbilityContext
    {
        /// <summary>
        /// 技能实例
        /// </summary>
        public AbilitySpec Spec { get; }

        /// <summary>
        /// 技能配置
        /// </summary>
        public GameplayAbility Ability => Spec?.ability;

        /// <summary>
        /// 施法者能力组件
        /// </summary>
        public AbilitySystemComponent Owner { get; }

        /// <summary>
        /// 施法者属性组件
        /// </summary>
        public StatController StatController { get; }

        /// <summary>
        /// 取消令牌
        /// </summary>
        public CancellationToken CancellationToken => Owner?.GetCancellationTokenOnDestroy() ?? default;

        /// <summary>
        /// 是否已经取消
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// 运行中任务列表
        /// </summary>
        private readonly List<AbilityTask> activeTaskList = new();

        /// <summary>
        /// 技能运行时上下文
        /// </summary>
        public AbilityContext(AbilitySpec spec, AbilitySystemComponent owner, StatController statController)
        {
            Spec = spec;
            Owner = owner;
            StatController = statController;
        }

        /// <summary>
        /// 注册运行时任务
        /// </summary>
        public T RegisterTask<T>(T task) where T : AbilityTask
        {
            if (task is null) return null;

            activeTaskList.Add(task);
            return task;
        }

        /// <summary>
        /// 取消所有运行时任务
        /// </summary>
        public void CancelActiveTasks()
        {
            IsCancelled = true;

            for (int i = activeTaskList.Count - 1; i >= 0; i--)
            {
                var task = activeTaskList[i];
                task?.InterruptTask();
            }

            activeTaskList.Clear();
        }
    }
}
