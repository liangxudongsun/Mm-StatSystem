using System;
using GAS.AbilitySystem;
using GAS.Component;
using GAS.Core;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using GAS.TagSystem;
using UnityEngine;

namespace GAS.Demo.Test
{
    /// <summary>
    /// GAS 测试用技能
    /// </summary>
    public class GASTestCounterAbility : GameplayAbility
    {
        /// <summary>
        /// 激活次数
        /// </summary>
        public int ActivateCount;

        /// <summary>
        /// 激活技能
        /// </summary>
        public override void Activate(AbilityContext context)
        {
            ActivateCount++;
        }
    }

    /// <summary>
    /// GAS 测试用中断技能
    /// </summary>
    public class GASTestCancelAbility : GameplayAbility
    {
        /// <summary>
        /// 最近上下文
        /// </summary>
        public AbilityContext LastContext;

        /// <summary>
        /// 激活技能
        /// </summary>
        public override void Activate(AbilityContext context)
        {
            LastContext = context;
        }
    }

    /// <summary>
    /// GAS 测试断言
    /// </summary>
    public static class GASTestAssert
    {
        /// <summary>
        /// 断言为真
        /// </summary>
        public static void True(GASTestReport report, string name, bool condition, string failMessage)
        {
            if (condition)
            {
                report.RecordPass(name);
            }
            else
            {
                report.RecordFail(name, failMessage);
            }
        }

        /// <summary>
        /// 断言近似相等
        /// </summary>
        public static void Approx(GASTestReport report, string name, float actual, float expected, float tolerance, string failMessage)
        {
            bool passed = Mathf.Abs(actual - expected) <= tolerance;
            if (passed)
            {
                report.RecordPass(name);
            }
            else
            {
                report.RecordFail(name, $"{failMessage} 期望 {expected} 实际 {actual}");
            }
        }
    }

    /// <summary>
    /// GAS 测试套件
    /// </summary>
    public static class GASTestSuites
    {
        /// <summary>
        /// 运行全部测试
        /// </summary>
        public static void RunAll(GASTestReport report)
        {
            RunStatSystemTests(report);
            RunTagSystemTests(report);
            RunGameplayEffectTests(report);
            RunAbilitySystemTests(report);
        }

        /// <summary>
        /// 属性系统测试
        /// </summary>
        public static void RunStatSystemTests(GASTestReport report)
        {
            StatData hpData = GASTestDataBuilder.CreateStatData("HP", E_StatType.Immediate, 100f, 0f, 200f);
            StatData attackData = GASTestDataBuilder.CreateStatData("Attack", E_StatType.Passive, 100f);
            GASTestSubject subject = GASTestSubject.Create("StatTestSubject", hpData, attackData);

            try
            {
                GASTestAssert.Approx(report, "Stat_ImStat初始值", subject.StatController.GetCurrentValue("HP"), 100f, 0.01f, "HP初始值错误");
                GASTestAssert.Approx(report, "Stat_Passive初始值", subject.StatController.GetCurrentValue("Attack"), 100f, 0.01f, "Attack初始值错误");

                subject.StatController.ChangeAttributeValue("HP", 20f, E_ModifierType.FlatAdd, subject.Root);
                GASTestAssert.Approx(report, "Stat_ImStat加法", subject.StatController.GetCurrentValue("HP"), 120f, 0.01f, "HP加法错误");

                subject.StatController.ChangeAttributeValue("HP", 200f, E_ModifierType.FlatAdd, subject.Root);
                GASTestAssert.Approx(report, "Stat_ImStat上限", subject.StatController.GetCurrentValue("HP"), 200f, 0.01f, "HP上限错误");

                subject.StatController.AddModifier("Attack", new StatModifier("flat", E_ModifierType.FlatAdd, 50f, subject.Root));
                GASTestAssert.Approx(report, "Stat_Passive平加", subject.StatController.GetCurrentValue("Attack"), 150f, 0.01f, "Attack平加错误");

                subject.StatController.AddModifier("Attack", new StatModifier("percent", E_ModifierType.PercentageAdd, 50f, subject.Root));
                GASTestAssert.Approx(report, "Stat_Passive百分比", subject.StatController.GetCurrentValue("Attack"), 225f, 0.01f, "Attack百分比错误");

                subject.StatController.AddModifier("Attack", new StatModifier("finalFlat", E_ModifierType.FinalAdd, 25f, subject.Root));
                subject.StatController.AddModifier("Attack", new StatModifier("finalPercent", E_ModifierType.FinalPercentage, 100f, subject.Root));
                GASTestAssert.Approx(report, "Stat_四阶段计算", subject.StatController.GetCurrentValue("Attack"), 500f, 0.01f, "四阶段计算错误");

                subject.StatController.RemoveModifiersFromSource("Attack", subject.Root);
                GASTestAssert.Approx(report, "Stat_移除修饰符", subject.StatController.GetCurrentValue("Attack"), 100f, 0.01f, "移除修饰符错误");
            }
            finally
            {
                subject.Destroy();
            }
        }

        /// <summary>
        /// 标签系统测试
        /// </summary>
        public static void RunTagSystemTests(GASTestReport report)
        {
            GameplayTag ailmentTag = new GameplayTag("Ailment");
            GameplayTag poisonTag = new GameplayTag("Ailment.Poison");
            GASTestAssert.True(report, "Tag_父子关系", ailmentTag.IsParentOf(poisonTag), "Ailment 应该是 Poison 父标签");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(new GameplayTag("Player"));
            container.AddTag(new GameplayTag("Ailment.Poison"));
            GASTestAssert.True(report, "Tag_父子匹配", container.ContainsTag(new GameplayTag("Ailment")), "容器应匹配父标签 Ailment");
            GASTestAssert.True(report, "Tag_ContainsAll", container.ContainsAll(new[] { new GameplayTag("Player"), new GameplayTag("Ailment.Poison") }), "ContainsAll 失败");
            GASTestAssert.True(report, "Tag_ContainsAny", container.ContainsAny(new[] { new GameplayTag("Enemy"), new GameplayTag("Player") }), "ContainsAny 失败");

            GameplayTagRequirements requirements = new GameplayTagRequirements();
            requirements.NeedTags.AddTag(new GameplayTag("Player"));
            requirements.BanTags.AddTag(new GameplayTag("Enemy"));

            GameplayTagContainer passContainer = new GameplayTagContainer();
            passContainer.AddTag(new GameplayTag("Player"));
            GASTestAssert.True(report, "Tag_需求满足", requirements.IsSatisfied(passContainer), "标签需求应满足");

            GameplayTagContainer failContainer = new GameplayTagContainer();
            failContainer.AddTags(new GameplayTag("Player"), new GameplayTag("Enemy"));
            GASTestAssert.True(report, "Tag_黑名单拦截", !requirements.IsSatisfied(failContainer), "黑名单应拦截");

            GASTestSubject subject = GASTestSubject.Create("TagTestSubject", GASTestDataBuilder.CreateStatData("HP", E_StatType.Immediate, 100f));
            try
            {
                subject.AbilitySystem.AddGameplayTag("State.Stunned");
                GASTestAssert.True(report, "Tag_ASC添加", subject.AbilitySystem.HasGameplayTag("State.Stunned"), "ASC 添加标签失败");
                subject.AbilitySystem.RemoveGameplayTag("State.Stunned");
                GASTestAssert.True(report, "Tag_ASC移除", !subject.AbilitySystem.HasGameplayTag("State.Stunned"), "ASC 移除标签失败");
                GASTestAssert.True(report, "Tag_ASC条件检查", subject.AbilitySystem.SatisfiesTagRequirements(new[] { "Faction.Player" }, new[] { "State.Stunned" }) == false, "缺少必需标签应失败");
            }
            finally
            {
                subject.Destroy();
            }
        }

        /// <summary>
        /// GE 系统测试
        /// </summary>
        public static void RunGameplayEffectTests(GASTestReport report)
        {
            StatData hpData = GASTestDataBuilder.CreateStatData("HP", E_StatType.Immediate, 100f, 0f, 100f);
            StatData attackData = GASTestDataBuilder.CreateStatData("Attack", E_StatType.Passive, 100f);
            GASTestSubject subject = GASTestSubject.Create("GETestSubject", hpData, attackData);

            try
            {
                GameplayEffectData instantHeal = GASTestDataBuilder.CreateGameplayEffect(
                    "TestInstantHeal",
                    E_EffectDuration.Instant,
                    GASTestDataBuilder.CreateModifierConfig("HP", E_ModifierType.FlatAdd, 30f));
                subject.TrackRuntimeAsset(instantHeal);

                GameplayEffectData instantDamage = GASTestDataBuilder.CreateGameplayEffect(
                    "TestInstantDamage",
                    E_EffectDuration.Instant,
                    GASTestDataBuilder.CreateModifierConfig("HP", E_ModifierType.FlatAdd, -20f));
                subject.TrackRuntimeAsset(instantDamage);
                subject.AbilitySystem.ApplyGE(instantDamage, subject.Root);
                GASTestAssert.Approx(report, "GE_即时伤害", subject.StatController.GetCurrentValue("HP"), 80f, 0.01f, "即时伤害错误");

                subject.AbilitySystem.ApplyGE(instantHeal, subject.Root);
                GASTestAssert.Approx(report, "GE_即时治疗", subject.StatController.GetCurrentValue("HP"), 100f, 0.01f, "即时治疗错误");

                GameplayEffectData durationBuff = GASTestDataBuilder.CreateGameplayEffect(
                    "TestDurationBuff",
                    E_EffectDuration.HasDuration,
                    2f,
                    false,
                    1f,
                    E_EffectStacking.None,
                    1,
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    GASTestDataBuilder.CreateModifierConfig("Attack", E_ModifierType.FlatAdd, 40f));
                subject.TrackRuntimeAsset(durationBuff);
                subject.AbilitySystem.ApplyGE(durationBuff, subject.Root);
                GASTestAssert.Approx(report, "GE_持续Buff", subject.StatController.GetCurrentValue("Attack"), 140f, 0.01f, "持续Buff未生效");

                subject.GEManager.UpdateGE(2.1f);
                GASTestAssert.Approx(report, "GE_持续到期移除", subject.StatController.GetCurrentValue("Attack"), 100f, 0.01f, "Buff到期后未移除");

                GameplayEffectData stackBuff = GASTestDataBuilder.CreateGameplayEffect(
                    "TestStackBuff",
                    E_EffectDuration.HasDuration,
                    5f,
                    false,
                    1f,
                    E_EffectStacking.StackUpper,
                    3,
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    GASTestDataBuilder.CreateModifierConfig("Attack", E_ModifierType.FlatAdd, 10f));
                subject.TrackRuntimeAsset(stackBuff);

                GameplayEffectSpec stackSpec1 = subject.AbilitySystem.ApplyGE(stackBuff, subject.Root);
                GameplayEffectSpec stackSpec2 = subject.AbilitySystem.ApplyGE(stackBuff, subject.Root);
                GASTestAssert.True(report, "GE_堆叠层数", stackSpec2 != null && stackSpec2.StackCount == 2, "堆叠层数应为2");
                GASTestAssert.Approx(report, "GE_堆叠数值", subject.StatController.GetCurrentValue("Attack"), 120f, 0.01f, "堆叠数值错误");

                subject.AbilitySystem.ApplyGE(stackBuff, subject.Root);
                subject.AbilitySystem.ApplyGE(stackBuff, subject.Root);
                GASTestAssert.True(report, "GE_堆叠上限", stackSpec2.StackCount == 3, "堆叠上限应为3");

                subject.GEManager.RemoveAllGE();
                subject.StatController.ChangeAttributeValue("HP", 100f, E_ModifierType.FlatAdd, subject.Root);

                GameplayEffectData dotEffect = GASTestDataBuilder.CreateGameplayEffect(
                    "TestDot",
                    E_EffectDuration.HasDuration,
                    3f,
                    true,
                    1f,
                    E_EffectStacking.None,
                    1,
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    GASTestDataBuilder.CreateModifierConfig("HP", E_ModifierType.FlatAdd, -10f));
                subject.TrackRuntimeAsset(dotEffect);
                subject.AbilitySystem.ApplyGE(dotEffect, subject.Root);
                subject.GEManager.UpdateGE(0f);
                subject.GEManager.UpdateGE(1.1f);
                subject.GEManager.UpdateGE(1.1f);
                GASTestAssert.Approx(report, "GE_周期伤害", subject.StatController.GetCurrentValue("HP"), 70f, 0.01f, "周期伤害错误");

                subject.GEManager.RemoveAllGE();
                subject.StatController.ChangeAttributeValue("HP", 30f, E_ModifierType.FlatAdd, subject.Root);

                GameplayEffectData tagBuff = GASTestDataBuilder.CreateGameplayEffect(
                    "TestTagBuff",
                    E_EffectDuration.HasDuration,
                    1.5f,
                    false,
                    1f,
                    E_EffectStacking.None,
                    1,
                    new[] { "State.Buff.Test" },
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    GASTestDataBuilder.CreateModifierConfig("Attack", E_ModifierType.FlatAdd, 5f));
                subject.TrackRuntimeAsset(tagBuff);
                subject.AbilitySystem.ApplyGE(tagBuff, subject.Root);
                GASTestAssert.True(report, "GE_授予Tag", subject.AbilitySystem.HasGameplayTag("State.Buff.Test"), "GE 应授予 Tag");
                subject.GEManager.UpdateGE(1.6f);
                GASTestAssert.True(report, "GE_到期移除Tag", !subject.AbilitySystem.HasGameplayTag("State.Buff.Test"), "GE 到期应移除 Tag");

                GameplayEffectData needTagEffect = GASTestDataBuilder.CreateGameplayEffect(
                    "TestNeedTag",
                    E_EffectDuration.Instant,
                    0f,
                    false,
                    1f,
                    E_EffectStacking.None,
                    1,
                    System.Array.Empty<string>(),
                    new[] { "State.Ready" },
                    System.Array.Empty<string>(),
                    GASTestDataBuilder.CreateModifierConfig("HP", E_ModifierType.FlatAdd, 5f));
                subject.TrackRuntimeAsset(needTagEffect);

                GameplayEffectSpec blockedSpec = subject.AbilitySystem.ApplyGE(needTagEffect, subject.Root);
                GASTestAssert.True(report, "GE_缺少必需Tag拦截", blockedSpec == null, "缺少必需 Tag 应拦截");

                subject.AbilitySystem.AddGameplayTag("State.Ready");
                GameplayEffectSpec allowedSpec = subject.AbilitySystem.ApplyGE(needTagEffect, subject.Root);
                GASTestAssert.True(report, "GE_满足必需Tag通过", allowedSpec != null, "满足必需 Tag 应通过");

                GameplayEffectData banTagEffect = GASTestDataBuilder.CreateGameplayEffect(
                    "TestBanTag",
                    E_EffectDuration.Instant,
                    0f,
                    false,
                    1f,
                    E_EffectStacking.None,
                    1,
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    new[] { "State.Stunned" },
                    GASTestDataBuilder.CreateModifierConfig("HP", E_ModifierType.FlatAdd, 5f));
                subject.TrackRuntimeAsset(banTagEffect);
                subject.AbilitySystem.AddGameplayTag("State.Stunned");
                GameplayEffectSpec banSpec = subject.AbilitySystem.ApplyGE(banTagEffect, subject.Root);
                GASTestAssert.True(report, "GE_禁止Tag拦截", banSpec == null, "禁止 Tag 应拦截 GE");
            }
            finally
            {
                subject.Destroy();
            }
        }

        /// <summary>
        /// 技能系统测试
        /// </summary>
        public static void RunAbilitySystemTests(GASTestReport report)
        {
            StatData hpData = GASTestDataBuilder.CreateStatData("HP", E_StatType.Immediate, 100f, 0f, 100f);
            StatData mpData = GASTestDataBuilder.CreateStatData("MP", E_StatType.Immediate, 50f, 0f, 50f);
            GASTestSubject subject = GASTestSubject.Create("AbilityTestSubject", hpData, mpData);

            try
            {
                GASTestCounterAbility counterAbility = ScriptableObject.CreateInstance<GASTestCounterAbility>();
                counterAbility.abilityName = "CounterAbility";
                counterAbility.cooldownTime = 2f;
                subject.TrackRuntimeAsset(counterAbility);

                AbilitySpec counterSpec = counterAbility.CreateAbilitySpec();
                GASTestAssert.True(report, "Ability_首次可激活", counterSpec.CanActivate(subject.StatController, subject.AbilitySystem), "首次应可激活");
                counterSpec.Activate(subject.AbilitySystem, subject.StatController);
                GASTestAssert.True(report, "Ability_激活成功", counterAbility.ActivateCount == 1, "激活次数应为1");
                GASTestAssert.True(report, "Ability_冷却拦截", !counterSpec.CanActivate(subject.StatController, subject.AbilitySystem), "冷却中应不可激活");

                GASTestCounterAbility costAbility = ScriptableObject.CreateInstance<GASTestCounterAbility>();
                costAbility.abilityName = "CostAbility";
                costAbility.costStatId = "MP";
                costAbility.costType = E_CostType.Fixed;
                costAbility.costValue = 20f;
                subject.TrackRuntimeAsset(costAbility);

                AbilitySpec costSpec = costAbility.CreateAbilitySpec();
                subject.StatController.ChangeAttributeValue("MP", -40f, E_ModifierType.FlatAdd, subject.Root);
                GASTestAssert.True(report, "Ability_消耗不足拦截", !costSpec.CanActivate(subject.StatController, subject.AbilitySystem), "MP不足应不可激活");

                subject.StatController.ChangeAttributeValue("MP", 40f, E_ModifierType.FlatAdd, subject.Root);
                costSpec.Activate(subject.AbilitySystem, subject.StatController);
                GASTestAssert.Approx(report, "Ability_消耗扣除", subject.StatController.GetCurrentValue("MP"), 30f, 0.01f, "消耗扣除错误");

                GASTestCounterAbility tagAbility = ScriptableObject.CreateInstance<GASTestCounterAbility>();
                tagAbility.abilityName = "TagAbility";
                GASTestReflectionHelper.SetField(tagAbility, "activationRequiredTags", new[] { "State.Ready" });
                GASTestReflectionHelper.SetField(tagAbility, "activationBlockedTags", new[] { "State.Stunned" });
                subject.TrackRuntimeAsset(tagAbility);

                AbilitySpec tagSpec = tagAbility.CreateAbilitySpec();
                GASTestAssert.True(report, "Ability_标签条件拦截", !tagSpec.CanActivate(subject.StatController, subject.AbilitySystem), "缺少必需标签应不可激活");
                subject.AbilitySystem.AddGameplayTag("State.Ready");
                GASTestAssert.True(report, "Ability_标签条件通过", tagSpec.CanActivate(subject.StatController, subject.AbilitySystem), "满足标签后应可激活");

                GASTestCancelAbility cancelAbility = ScriptableObject.CreateInstance<GASTestCancelAbility>();
                cancelAbility.abilityName = "CancelAbility";
                subject.TrackRuntimeAsset(cancelAbility);

                AbilitySpec cancelSpec = cancelAbility.CreateAbilitySpec();
                cancelSpec.Activate(subject.AbilitySystem, subject.StatController);
                cancelSpec.Interrupt();
                GASTestAssert.True(report, "AbilityContext_中断上下文", cancelAbility.LastContext != null && cancelAbility.LastContext.IsCancelled, "上下文中断失败");

                GASTestCounterAbility sharedAbility = ScriptableObject.CreateInstance<GASTestCounterAbility>();
                sharedAbility.abilityName = "SharedAbility";
                subject.TrackRuntimeAsset(sharedAbility);

                GASTestSubject subjectB = GASTestSubject.Create("AbilityTestSubjectB", hpData, mpData);
                try
                {
                    AbilitySpec sharedSpecA = sharedAbility.CreateAbilitySpec();
                    AbilitySpec sharedSpecB = sharedAbility.CreateAbilitySpec();
                    sharedSpecA.Activate(subject.AbilitySystem, subject.StatController);
                    sharedSpecB.Activate(subjectB.AbilitySystem, subjectB.StatController);
                    GASTestAssert.True(report, "Ability_SO共享不串状态", sharedAbility.ActivateCount == 2, "同一SO多实例激活应互不影响");
                }
                finally
                {
                    subjectB.Destroy();
                }
            }
            finally
            {
                subject.Destroy();
            }
        }
    }
}
