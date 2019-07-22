﻿using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.Visual.HitSystem;
using System;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Newtonsoft.Json;
using Kingmaker.Utility;
using Kingmaker.UI.GenericSlot;
using Kingmaker.Items;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.EntitySystem.Entities;
using System.Collections.Generic;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.Blueprints.Validation;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.ElementsSystem;
using Kingmaker.Controllers;
using Kingmaker;
using static Kingmaker.UnitLogic.Abilities.Components.AbilityCustomMeleeAttack;
using Kingmaker.UnitLogic.Mechanics.ContextData;
using Kingmaker.Controllers.Projectiles;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.UnitLogic.ActivatableAbilities;

namespace CallOfTheWild
{
    namespace NewMechanics
    {
        [ComponentName("Apply metamagic for resource")]
        [AllowedOn(typeof(Kingmaker.Blueprints.Facts.BlueprintUnitFact))]
        public class MetaRage : RuleInitiatorLogicComponent<RuleCastSpell>, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IRulebookHandler<RuleCalculateAbilityParams>
        {
            public Metamagic metamagic;
            public BlueprintAbilityResource resource;
            private int cost = 0;

            public MetaRage(Metamagic metamagic_to_apply, BlueprintAbilityResource resource_to_use)
            {
                metamagic = metamagic_to_apply;
                resource = resource_to_use;
            }


            public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                bool is_metamagic_available = ((evt.Spell.AvailableMetamagic & metamagic) != 0);
                if (evt.Spell == null || evt.Spellbook == null || evt.Spell.Type != AbilityType.Spell || !is_metamagic_available)
                {
                    return;
                }

                cost = 2 * (evt.Spellbook.GetSpellLevel(evt.Spell) + MetamagicHelper.DefaultCost(metamagic));
                if (this.resource == null || this.Owner.Resources.GetResourceAmount((BlueprintScriptableObject)this.resource) < cost)
                {
                    cost = 0;
                    return;
                }
                evt.AddMetamagic(this.metamagic);
            }

            public override void OnEventAboutToTrigger(RuleCastSpell evt)
            {

            }

            public override void OnEventDidTrigger(RuleCastSpell evt)
            {
                if (cost == 0)
                {
                    return;
                }
                this.Owner.Resources.Spend((BlueprintScriptableObject)this.resource, cost);
            }



            public void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {
            }
        }

        [ComponentName("Increase spell descriptor DC by spell level up to BonusDC and then deals dc_increase d6 damage")]
        [AllowedOn(typeof(Kingmaker.Blueprints.Facts.BlueprintUnitFact))]
        public class RageCasting : RuleInitiatorLogicComponent<RuleCastSpell>, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IRulebookHandler<RuleCalculateAbilityParams>
        {
            public int BonusDC;
            private int actual_dc = 0;

            public override void OnEventAboutToTrigger(RuleCastSpell evt)
            {

            }

            public void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {

            }


            public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                bool no_save = evt.Spell.EffectOnEnemy != AbilityEffectOnUnit.Harmful; //TODO: properly check for saving throw
                if (evt.Spell == null || evt.Spellbook == null || evt.Spell.Type != AbilityType.Spell || no_save)
                {
                    return;
                }
                actual_dc = Mathf.Min(evt.Spellbook.GetSpellLevel(evt.Spell), BonusDC);
                evt.AddBonusDC(actual_dc);
            }

            public override void OnEventDidTrigger(RuleCastSpell evt)
            {
                if (actual_dc == 0)
                {
                    return;
                }
                Common.AddBattleLogMessage($"{Owner.CharacterName}: Rage Casting increases spell DC by {actual_dc}");
                RuleDealDamage evt_dmg = new RuleDealDamage(this.Owner.Unit, this.Owner.Unit, new DamageBundle(new BaseDamage[1]
                {
                (BaseDamage) new EnergyDamage(new DiceFormula(actual_dc, DiceType.D6), Kingmaker.Enums.Damage.DamageEnergyType.Holy)
                }));
                evt_dmg.Reason = (RuleReason)this.Fact;
                //temporary remove temp hp
                var temp_hp_modifiers = this.Owner.Stats.TemporaryHitPoints.Modifiers.ToArray();
                foreach (var m in temp_hp_modifiers)
                {
                    this.Owner.Stats.TemporaryHitPoints.RemoveModifier(m);
                }
                this.Owner.Stats.TemporaryHitPoints.UpdateValue();
                Rulebook.Trigger<RuleDealDamage>(evt_dmg);
                if (this.Owner.HPLeft <= 0)
                { //do not give hp back if owner is unconscious
                    return;
                }
                foreach (var m in temp_hp_modifiers)
                {
                    this.Owner.Stats.TemporaryHitPoints.AddModifier(m.ModValue, m.Source, m.SourceComponent, m.ModDescriptor);
                }
                this.Owner.Stats.TemporaryHitPoints.UpdateValue();
            }
        }


        [ComponentName("Increase caster level by value and apply on caster debuff for duration equal to rate*spell_level if it fails saving throw against (dc_base + spell_level + caster_level_increase)")]
        [AllowedOn(typeof(Kingmaker.Blueprints.Facts.BlueprintUnitFact))]
        public class ConduitSurge : RuleInitiatorLogicComponent<RuleCastSpell>, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IRulebookHandler<RuleCalculateAbilityParams>
        {
            public BlueprintBuff buff;
            public DurationRate rate = DurationRate.Rounds;
            public ContextDiceValue dice_value;
            public SavingThrowType save_type;
            public int dc_base = 10;
            public string display_name = "Conduit Surge";
            public BlueprintAbilityResource resource;
            private int caster_level_increase = -1;


            public override void OnEventAboutToTrigger(RuleCastSpell evt)
            {

            }


            public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                caster_level_increase = -1;
                if (evt.Spell == null || evt.Spellbook == null || evt.Spell.Type != AbilityType.Spell)
                {
                    return;
                }
                caster_level_increase = dice_value.Calculate(this.Fact.MaybeContext);
                evt.AddBonusCasterLevel(caster_level_increase);
            }

            public void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {

            }

            public override void OnEventDidTrigger(RuleCastSpell evt)
            {
                if (caster_level_increase == -1)
                {
                    return;
                }
                Common.AddBattleLogMessage($"{Owner.CharacterName}: {display_name} increases caster level by {caster_level_increase}");
                RuleSavingThrow ruleSavingThrow = this.Fact.MaybeContext.TriggerRule<RuleSavingThrow>(new RuleSavingThrow(this.Owner.Unit, save_type, dc_base + evt.Spell.SpellLevel + caster_level_increase));
                if (!ruleSavingThrow.IsPassed)
                {
                    this.Owner.Buffs.AddBuff(buff, this.Owner.Unit, (rate.ToRounds() * evt.Spell.SpellLevel).Seconds);
                }
                if (resource != null)
                {
                    this.Owner.Resources.Spend((BlueprintScriptableObject)this.resource, 1);
                }
            }
        }


        [AllowedOn(typeof(BlueprintBuff))]
        [ComponentName("Buffs/AddEffect/ContextFastHealing")]
        public class AddContextEffectFastHealing : BuffLogic, ITickEachRound, ITargetRulebookHandler<RuleDealDamage>, IRulebookHandler<RuleDealDamage>, ITargetRulebookSubscriber
        {
            public int Multiplier = 1;
            public ContextValue Value;

            public void OnNewRound()
            {
                int heal_amount = this.Value.Calculate(this.Context);
                if (this.Owner.State.IsDead || this.Owner.Damage <= 0)
                {
                    return;
                }
                GameHelper.HealDamage(this.Owner.Unit, this.Owner.Unit, heal_amount * Multiplier);
            }

            public void OnEventAboutToTrigger(RuleDealDamage evt)
            {
            }

            public void OnEventDidTrigger(RuleDealDamage evt)
            {
            }
        }


        [AllowMultipleComponents]
        [AllowedOn(typeof(BlueprintUnitFact))]
        [ComponentName("Saving throw bonus against allies")]
        public class SavingThrowBonusAgainstAllies : RuleInitiatorLogicComponent<RuleSavingThrow>
        {
            public ModifierDescriptor Descriptor;
            public int Value;

            public override void OnEventAboutToTrigger(RuleSavingThrow evt)
            {
                var caster = evt.Reason.Caster;
                if (caster == null || caster.IsPlayersEnemy)
                    return;
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveWill.AddModifier(this.Value * this.Fact.GetRank(), (GameLogicComponent)this, this.Descriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveReflex.AddModifier(this.Value * this.Fact.GetRank(), (GameLogicComponent)this, this.Descriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveFortitude.AddModifier(this.Value * this.Fact.GetRank(), (GameLogicComponent)this, this.Descriptor));
            }

            public override void OnEventDidTrigger(RuleSavingThrow evt)
            {
            }
        }


        public class BuffContextEnchantPrimaryHandWeapon : BuffLogic
        {
            public BlueprintWeaponEnchantment[] enchantments;
            public ContextValue value;
            public BlueprintWeaponType[] allowed_types;
            public bool lock_slot = false;
            public bool only_non_magical = false;
            [JsonProperty]
            private ItemEnchantment m_Enchantment;
            [JsonProperty]
            private ItemEntityWeapon m_Weapon;
            [JsonProperty]
            private bool m_unlock;


            public override void OnFactActivate()
            {
                m_unlock = false;
                var unit = this.Owner;
                if (unit == null) return;

                var weapon = unit.Body.PrimaryHand.MaybeWeapon;
                if (weapon == null)
                {
                    return;
                }

                if (!allowed_types.Empty() && !allowed_types.Contains(weapon.Blueprint.Type))
                {
                    return;
                }

                int bonus = value.Calculate(Context) - 1;
                if (bonus < 0)
                {
                    bonus = 0;
                }
                if (bonus >= enchantments.Length)
                {
                    bonus = enchantments.Length - 1;
                }

                var fact = weapon.Enchantments.Find(x => x.Blueprint == enchantments[bonus]);
                if (fact != null)
                {
                    weapon.RemoveEnchantment(fact);
                }

                if (weapon.EnchantmentValue != 0  && only_non_magical)
                {
                    return;
                }
                m_Enchantment = weapon.AddEnchantment(enchantments[bonus], Context, new Rounds?());

                if (lock_slot && !weapon.IsNonRemovable)
                {
                    weapon.IsNonRemovable = true;
                    m_unlock = true;
                }
                //m_Enchantment.RemoveOnUnequipItem = remove_on_unequip;
                m_Weapon = weapon;
            }

            public override void OnFactDeactivate()
            {
                if (this.m_Weapon == null)
                    return;
                //m_Weapon.IsNonRemovable = false;
                if (m_unlock)
                {
                    m_Weapon.IsNonRemovable = false;
                }
                if (this.m_Enchantment == null)
                    return;
                this.m_Enchantment.Owner?.RemoveEnchantment(this.m_Enchantment);
            }
        }



        public class BuffContextEnchantPrimaryHandWeaponIfHasMetamagic : BuffLogic
        {
            public BlueprintWeaponEnchantment enchantment;
            public Metamagic metamagic;
            public BlueprintWeaponType[] allowed_types;
            public bool lock_slot = false;
            public bool only_non_magical = false;
            [JsonProperty]
            private ItemEnchantment m_Enchantment;
            [JsonProperty]
            private ItemEntityWeapon m_Weapon;
            [JsonProperty]
            private bool m_unlock;


            public override void OnFactActivate()
            {
                m_unlock = false;
                var unit = this.Owner;
                if (unit == null) return;

                var weapon = unit.Body.PrimaryHand.MaybeWeapon;
                if (weapon == null)
                {
                    return;
                }

                if (!allowed_types.Empty() && !allowed_types.Contains(weapon.Blueprint.Type))
                {
                    return;
                }

                if (!Context.HasMetamagic(metamagic))
                {
                    return;
                }

                var fact = weapon.Enchantments.Find(x => x.Blueprint == enchantment);
                if (fact != null)
                {
                    weapon.RemoveEnchantment(fact);
                }

                if (weapon.EnchantmentValue != 0 && only_non_magical)
                {
                    return;
                }

                m_Enchantment = weapon.AddEnchantment(enchantment, Context, new Rounds?());

                if (lock_slot && !weapon.IsNonRemovable)
                {
                    weapon.IsNonRemovable = true;
                    m_unlock = true;
                }
                //m_Enchantment.RemoveOnUnequipItem = remove_on_unequip;
                m_Weapon = weapon;
            }

            public override void OnFactDeactivate()
            {
                if (this.m_Weapon == null)
                    return;
                //m_Weapon.IsNonRemovable = false;
                if (m_unlock)
                {
                    m_Weapon.IsNonRemovable = false;
                }
                if (this.m_Enchantment == null)
                    return;
                this.m_Enchantment.Owner?.RemoveEnchantment(this.m_Enchantment);
            }
        }


        public class BuffContextEnchantShield : BuffLogic
        {
            public BlueprintArmorEnchantment[] enchantments;
            public ContextValue value;
            [JsonProperty]
            private ItemEnchantment m_Enchantment;
            [JsonProperty]
            private ItemEntityShield m_Shield;

            public override void OnFactActivate()
            {
                var unit = this.Owner;
                if (unit == null) return;

                var shield = unit.Body.SecondaryHand.MaybeShield;
                if (shield == null)
                {
                    return;
                }

                int bonus = value.Calculate(Context) - 1;
                if (bonus < 0)
                {
                    bonus = 0;
                }
                if (bonus >= enchantments.Length)
                {
                    bonus = enchantments.Length - 1;
                }

                var fact = shield.ArmorComponent.Enchantments.Find(x => x.Blueprint == enchantments[bonus]);
                if (fact != null)
                {
                    shield.RemoveEnchantment(fact);
                }

                m_Enchantment = shield.ArmorComponent.AddEnchantment(enchantments[bonus], Context, new Rounds?());
                shield.ArmorComponent.RecalculateStats();
                m_Shield = shield;
            }

            public override void OnFactDeactivate()
            {
                if (this.m_Enchantment == null)
                    return;
                this.m_Enchantment.Owner?.RemoveEnchantment(this.m_Enchantment);
                if (m_Shield != null)
                {
                    m_Shield.ArmorComponent.RecalculateStats();
                }
            }
        }


        public class BuffContextEnchantArmor : BuffLogic
        {
            public BlueprintArmorEnchantment[] enchantments;
            public ContextValue value;
            [JsonProperty]
            private ItemEnchantment m_Enchantment;
            [JsonProperty]
            private ItemEntityArmor m_Armor;

            public override void OnFactActivate()
            {
                var unit = this.Owner;
                if (unit == null) return;

                var armor = unit.Body.Armor.MaybeArmor;
                if (armor == null) return;

                int bonus = value.Calculate(Context) - 1;
                if (bonus < 0)
                {
                    bonus = 0;
                }
                if (bonus >= enchantments.Length)
                {
                    bonus = enchantments.Length - 1;
                }

                var fact = armor.Enchantments.Find(x => x.Blueprint == enchantments[bonus]);
                if (fact != null )
                {
                    armor.RemoveEnchantment(fact);
                }

                m_Enchantment = armor.AddEnchantment(enchantments[bonus], Context, new Rounds?());

                armor.RecalculateStats();
                m_Armor = armor;
            }

            public override void OnFactDeactivate()
            {
                if (this.m_Enchantment == null)
                    return;
                this.m_Enchantment.Owner?.RemoveEnchantment(this.m_Enchantment);
                if (m_Armor != null)
                {
                    m_Armor.RecalculateStats();
                }
            }
        }


        [AllowMultipleComponents]
        [ComponentName("Predicates/Target has fact unless alternative")]
        [AllowedOn(typeof(BlueprintAbility))]
        public class AbilityTargetHasNoFactUnlessBuffsFromCaster : BlueprintComponent, IAbilityTargetChecker
        {
            public BlueprintBuff[] CheckedBuffs;
            public BlueprintBuff[] AlternativeBuffs;


            public bool CanTarget(UnitEntityData caster, TargetWrapper target)
            {
                UnitEntityData unit = target.Unit;
                if (unit == null)
                    return false;
                bool flag1 = false;

                foreach (var CheckedBuff in this.CheckedBuffs)
                {
                    foreach (var b in unit.Descriptor.Buffs)
                    {
                        flag1 = (b.Blueprint == CheckedBuff) && (b.MaybeContext.MaybeCaster == caster);
                        if (flag1) break;
                    }
                    if (flag1) break;
                }

                bool flag2 = false;
                foreach (var AlternativeBuff in this.AlternativeBuffs)
                {
                    foreach (var b in unit.Descriptor.Buffs)
                    {
                        flag2 = (b.Blueprint == AlternativeBuff) && (b.MaybeContext.MaybeCaster == caster);
                        if (flag2) break;
                    }
                    if (flag2) break;
                }

                if (flag1)
                {
                    return flag2;
                }
                return true;
            }
        }


        [AllowedOn(typeof(BlueprintUnitFact))]
        public class SavingThrowBonusAgainstSpecificSpells : RuleInitiatorLogicComponent<RuleSavingThrow>
        {
            public BlueprintAbility[] Spells;
            public ModifierDescriptor ModifierDescriptor;
            public int Value;
            public BlueprintUnitFact[] BypassFeatures;

            public override void OnEventAboutToTrigger(RuleSavingThrow evt)
            {
                BlueprintAbility sourceAbility = evt.Reason.Context?.SourceAbility;
                UnitEntityData maybeCaster = evt.Reason.Context?.MaybeCaster;
                bool flag = maybeCaster != null;
                if (flag)
                {
                    flag = false;
                    foreach (BlueprintUnitFact bypassFeature in this.BypassFeatures)
                        flag = maybeCaster.Descriptor.HasFact(bypassFeature);
                }
                if (!(sourceAbility != null) || !((IEnumerable<BlueprintAbility>)this.Spells).Contains<BlueprintAbility>(sourceAbility) || flag)
                    return;

                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveWill.AddModifier(this.Value, (GameLogicComponent)this, this.ModifierDescriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveReflex.AddModifier(this.Value, (GameLogicComponent)this, this.ModifierDescriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveFortitude.AddModifier(this.Value, (GameLogicComponent)this, this.ModifierDescriptor));
            }

            public override void OnEventDidTrigger(RuleSavingThrow evt)
            {
            }
        }


        public class ContextCalculateAbilityParamsBasedOnClasses : ContextAbilityParamsCalculator
        {
            public StatType StatType = StatType.Charisma;
            public BlueprintCharacterClass[] CharacterClasses;

            public override AbilityParams Calculate(MechanicsContext context)
            {
                UnitEntityData maybeCaster = context.MaybeCaster;
                if (maybeCaster == null)
                {
                    return context.Params;
                }
                StatType statType = this.StatType;

                AbilityData ability = context.SourceAbilityContext?.Ability;
                RuleCalculateAbilityParams rule = !(ability != (AbilityData)null) ? new RuleCalculateAbilityParams(maybeCaster, context.AssociatedBlueprint, (Spellbook)null) : new RuleCalculateAbilityParams(maybeCaster, ability);
                rule.ReplaceStat = new StatType?(statType);

                int class_level = 0;
                foreach (var c in this.CharacterClasses)
                {
                    class_level += maybeCaster.Descriptor.Progression.GetClassLevel(c);
                }
                rule.ReplaceCasterLevel = new int?(class_level);
                rule.ReplaceSpellLevel = new int?(class_level / 2);
                return context.TriggerRule<RuleCalculateAbilityParams>(rule).Result;
            }

            public override void Validate(ValidationContext context)
            {
                base.Validate(context);
                if (this.StatType.IsAttribute() || this.StatType == StatType.BaseAttackBonus)
                    return;
                string str = string.Join(", ", ((IEnumerable<StatType>)StatTypeHelper.Attributes).Select<StatType, string>((Func<StatType, string>)(s => s.ToString())));
                context.AddError("StatType must be Base Attack Bonus or an attribute: {0}", (object)str);
            }
        }



        public class ContextActionResurrectInstant : ContextAction
        {
            public bool FullRestore;
            [HideIf("FullRestore")]
            public float ResultHealth = 0.5f;

            public override string GetCaption()
            {
                return "Resurrect";
            }

            public override void RunAction()
            {
                UnitEntityData unit = this.Target.Unit;
                if (unit != null && this.Context.MaybeCaster != null)
                {
                    UnitEntityData pair = UnitPartDualCompanion.GetPair(unit);
                    if (this.FullRestore)
                    {
                        unit.Descriptor.ResurrectAndFullRestore();
                        pair?.Descriptor.ResurrectAndFullRestore();
                    }
                    else
                    {
                        unit.Descriptor.Resurrect(this.ResultHealth, true);
                        pair?.Descriptor.Resurrect(this.ResultHealth, true);
                    }

                }
            }
        }


        [AllowedOn(typeof(BlueprintUnitFact))]
        [AllowMultipleComponents]
        public class CrowdAlliesACBonus : RuleTargetLogicComponent<RuleCalculateAC>
        {
            public int num_allies_around;
            public int Radius;
            public ContextValue value;

            public override void OnEventAboutToTrigger(RuleCalculateAC evt)
            {
                int num = 0;
                foreach (UnitEntityData unitEntityData in GameHelper.GetTargetsAround(this.Owner.Unit.Position, (float)this.Radius, true, false))
                {
                    if ((unitEntityData != this.Owner.Unit && !unitEntityData.IsEnemy(this.Owner.Unit)))
                    {
                        num++;
                    }
                }
                if (num < num_allies_around)
                {
                    return;
                }
                var ac_bonus = value.Calculate(this.Fact.MaybeContext);
                evt.AddBonus(ac_bonus, this.Fact);
            }

            public override void OnEventDidTrigger(RuleCalculateAC evt)
            {
            }
        }


        [ComponentName("Increase context spells DC by descriptor")]
        [AllowedOn(typeof(BlueprintUnitFact))]
        public class ContextIncreaseDescriptorSpellsDC : RuleInitiatorLogicComponent<RuleCalculateAbilityParams>
        {
            public ContextValue Value;
            public SpellDescriptorWrapper Descriptor;

            private MechanicsContext Context
            {
                get
                {
                    MechanicsContext context = (this.Fact as Buff)?.Context;
                    if (context != null)
                        return context;
                    return (this.Fact as Feature)?.Context;
                }
            }

            public override void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                bool? nullable = evt.Spell.GetComponent<SpellDescriptorComponent>()?.Descriptor.HasAnyFlag((SpellDescriptor)this.Descriptor);
                if (!nullable.HasValue || !nullable.Value)
                    return;
                evt.AddBonusDC(this.Value.Calculate(this.Context));
            }

            public override void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {
            }
        }



        [AllowedOn(typeof(BlueprintBuff))]
        [ComponentName("Buffs/Damage bonus for specific weapon types")]
        public class ContextWeaponTypeDamageBonus : RuleInitiatorLogicComponent<RuleCalculateWeaponStats>
        {
            public BlueprintWeaponType[] weapon_types;
            public ContextValue Value;

            public override void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
            {
                int num = Value.Calculate(this.Fact.MaybeContext);
                foreach (var w in weapon_types)
                {
                    if (evt.Weapon.Blueprint.Type == w)
                    {
                        evt.AddBonusDamage(num);
                        return;
                    }
                }

            }

            public override void OnEventDidTrigger(RuleCalculateWeaponStats evt)
            {
            }
        }


        public class ContextActionRemoveBuffFromCaster : ContextAction
        {
            public BlueprintBuff Buff;

            public override string GetCaption()
            {
                return "Remove Buff From Caster: " + this.Buff.Name;
            }

            public override void RunAction()
            {
                MechanicsContext context = ElementsContext.GetData<MechanicsContext.Data>()?.Context;
                if (context == null)
                    return;
                UnitEntityData maybeCaster = this.Context.MaybeCaster;
                foreach (var b in this.Target.Unit.Buffs)
                {
                    if (b.Blueprint == Buff && b.Context.MaybeCaster == maybeCaster)
                    {
                        this.Target.Unit.Buffs.RemoveFact((BlueprintFact)this.Buff);
                    }
                }
            }
        }


        //gives target immunity to buff unless target is caster
        [AllowMultipleComponents]
        [AllowedOn(typeof(BlueprintUnitFact))]
        public class SpecificBuffImmunityExceptCaster : RuleInitiatorLogicComponent<RuleApplyBuff>
        {
            public BlueprintBuff Buff;
            public bool except_caster = false;

            public override void OnEventAboutToTrigger(RuleApplyBuff evt)
            {
                if (evt.Context.MaybeCaster == this.Owner.Unit && except_caster)
                    return;
                if (evt.Blueprint != this.Buff)
                    return;
                evt.CanApply = false;
            }

            public override void OnEventDidTrigger(RuleApplyBuff evt)
            {
            }
        }



        [AllowMultipleComponents]
        [AllowedOn(typeof(BlueprintUnitFact))]
        public class ComeAndGetMe : RuleTargetLogicComponent<RuleCalculateAC>, ITargetRulebookHandler<RuleDealDamage>
        {
            public override void OnEventAboutToTrigger(RuleCalculateAC evt)
            {
                if (this.Owner.Body.PrimaryHand.MaybeWeapon.Blueprint.IsMelee)
                {
                    //this.Owner.Unit.CombatState.AttackOfOpportunity(evt.Initiator);
                    Game.Instance.CombatEngagementController.ForceAttackOfOpportunity(this.Owner.Unit, evt.Initiator);
                }
                evt.AddBonus(-4, this.Fact);
                return;
            }
            public override void OnEventDidTrigger(RuleCalculateAC evt)
            {
            }


            public void OnEventAboutToTrigger(RuleDealDamage evt)
            {
                if (evt.DamageBundle.Count() > 0 && evt.Reason.Rule is RuleAttackWithWeapon)
                {
                    evt.DamageBundle.ElementAt(0).AddBonus(4);
                }

            }
            public void OnEventDidTrigger(RuleDealDamage evt)
            {
            }
        }


        public class WeaponTypeSizeChange : RuleInitiatorLogicComponent<RuleCalculateWeaponStats>
        {
            public int SizeCategoryChange;
            public BlueprintWeaponType[] WeaponTypes;

            public override void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
            {
                if (!this.WeaponTypes.Contains(evt.Weapon.Blueprint.Type) || this.SizeCategoryChange == 0)
                    return;
                if (this.SizeCategoryChange > 0)
                {
                    for (int i = 0; i < this.SizeCategoryChange; i++)
                    {
                        evt.IncreaseWeaponSize();
                    }
                }
                else
                {
                    for (int i = 0; i > this.SizeCategoryChange; i--)
                    {
                        evt.DecreaseWeaponSize();
                    }
                }
            }

            public override void OnEventDidTrigger(RuleCalculateWeaponStats evt)
            {
            }
        }


        [AllowedOn(typeof(BlueprintUnitFact))]
        [AllowMultipleComponents]
        public class ContextWeaponDamageBonus : RuleInitiatorLogicComponent<RuleCalculateWeaponStats>
        {
            public ContextValue value;
            public bool apply_to_melee = true;
            public bool apply_to_ranged = false;
            public bool apply_to_thrown = true;
            public bool scale_for_2h = true;


            private MechanicsContext Context
            {
                get
                {
                    MechanicsContext context = (this.Fact as Buff)?.Context;
                    if (context != null)
                        return context;
                    return (this.Fact as Feature)?.Context;
                }
            }

            public override void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
            {
                int damage_bonus = value.Calculate(this.Context);
                if (damage_bonus <= 0)
                {
                    return;
                }

                var weapon = evt.Weapon;
                if (weapon == null)
                {
                    return;
                }
                if (weapon.Blueprint.IsMelee && !apply_to_melee
                    || weapon.Blueprint.IsRanged && !apply_to_ranged && weapon.Blueprint.FighterGroup != WeaponFighterGroup.Thrown
                    || weapon.Blueprint.FighterGroup == WeaponFighterGroup.Thrown && !apply_to_thrown)
                {
                    return;
                }
                if (scale_for_2h
                    && (weapon.Blueprint.IsTwoHanded || (weapon.Blueprint.IsOneHandedWhichCanBeUsedWithTwoHands && !evt.Initiator.Body.SecondaryHand.HasItem))
                    )
                {
                    damage_bonus += damage_bonus / 2;
                }
                evt.AddBonusDamage(damage_bonus);
            }

            public override void OnEventDidTrigger(RuleCalculateWeaponStats evt)
            {
            }
        }

        [AllowedOn(typeof(BlueprintFeature))]
        [AllowedOn(typeof(BlueprintBuff))]
        [AllowMultipleComponents]
        public class VitalStrikeScalingDamage : OwnedGameLogicComponent<UnitDescriptor>
        {
            public ContextValue Value;
            public int multiplier = 1;
            private MechanicsContext Context
            {
                get
                {
                    return this.Fact.MaybeContext;
                }
            }

        }



        [Harmony12.HarmonyPatch(typeof(VitalStrike))]
        [Harmony12.HarmonyPatch("OnEventDidTrigger", Harmony12.MethodType.Normal)]
        class VitalStrike__OnEventDidTrigger__Patch
        {
            static void Postfix(VitalStrike __instance, RuleCalculateWeaponStats evt, ref int ___m_DamageMod)
            {
                DamageDescription damageDescription = evt.DamageDescription.FirstItem<DamageDescription>();
                if (damageDescription == null || damageDescription.TypeDescription.Type != DamageType.Physical)
                    return;

                int bonus = 0;
                foreach (var b in evt.Initiator.Buffs)
                {
                    var dmg = b.Get<VitalStrikeScalingDamage>();
                    if (dmg == null || b.Context == null)
                    {
                        continue;
                    }
                    bonus += dmg.Value.Calculate(b.Context) * dmg.multiplier;
                }
                bonus *= ___m_DamageMod - 1;
                if (bonus <= 0)
                {
                    return;
                }
                damageDescription.Bonus += bonus;
            }
        }


        [AllowedOn(typeof(BlueprintUnitFact))]
        public class ForbidSpellCastingUnlessHasClass : OwnedGameLogicComponent<UnitDescriptor>
        {
            public bool ForbidMagicItems;
            public BlueprintCharacterClass[] allowed_classes;
            private bool activated = false;

            public override void OnTurnOn()
            {
                foreach (var c in allowed_classes)
                {
                    foreach (Kingmaker.UnitLogic.ClassData classData in this.Owner.Progression.Classes)
                    {
                        if (classData.CharacterClass == c)
                        {
                            return;
                        }
                    }
                }

                activated = true;
                this.Owner.State.SpellCastingForbidden.Retain();
                if (!this.ForbidMagicItems)
                    return;
                this.Owner.State.MagicItemsForbidden.Retain();
            }

            public override void OnTurnOff()
            {
                if (!activated)
                {
                    return;
                }
                activated = false;
                this.Owner.State.SpellCastingForbidden.Release();
                if (!this.ForbidMagicItems)
                    return;
                this.Owner.State.MagicItemsForbidden.Release();
            }
        }

        [AllowedOn(typeof(BlueprintUnitFact))]
        public class ReflectDamage : OwnedGameLogicComponent<UnitDescriptor>, IInitiatorRulebookHandler<RuleDealDamage>, IRulebookHandler<RuleDealDamage>, IInitiatorRulebookSubscriber
        {
            
            public bool reflect_melee_weapon = false;
            public bool reflect_ranged_weapon = false;
            public bool reflect_magic = false;

            public float reflection_coefficient = 0.0f;

            public void OnEventAboutToTrigger(RuleDealDamage evt)
            {

            }

            public void OnEventDidTrigger(RuleDealDamage evt)
            {
                if (evt.Target == evt.Initiator)
                {
                    return;
                }
                if ((evt.Reason.Rule is RuleAttackWithWeapon))
                {
                    var rule_attack_with_weapon = (RuleAttackWithWeapon)evt.Reason.Rule;
                    bool is_melee = (rule_attack_with_weapon.Weapon == null || rule_attack_with_weapon.Weapon.Blueprint.IsMelee);
                    bool is_ranged = (rule_attack_with_weapon.Weapon != null && rule_attack_with_weapon.Weapon.Blueprint.IsRanged);
                    if (is_melee && !reflect_melee_weapon)
                    {
                        return;
                    }
                    
                    if (is_ranged && !reflect_ranged_weapon)
                    {
                        return;
                    }
                }
                else
                {
                    if (!reflect_magic)
                    {
                        return;
                    }
                }

                int reflected_dmage = (int)(reflection_coefficient * evt.Damage);
                if (reflected_dmage <= 0)
                {
                    return;
                }

                var base_dmg = new EnergyDamage(DiceFormula.Zero, Kingmaker.Enums.Damage.DamageEnergyType.Holy);
                base_dmg.AddBonus(reflected_dmage);

                RuleDealDamage evt_dmg = new RuleDealDamage(this.Owner.Unit, this.Owner.Unit, new DamageBundle(base_dmg));
                Rulebook.Trigger<RuleDealDamage>(evt_dmg);
            }
        }


        [ComponentName("Weapon Attack Stat Replacement")]
        public class   WeaponAttackStatReplacement : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RuleCalculateAttackBonusWithoutTarget>, IRulebookHandler<RuleCalculateAttackBonusWithoutTarget>, IInitiatorRulebookSubscriber
        {
            public StatType Stat;

            public void OnEventAboutToTrigger(RuleCalculateAttackBonusWithoutTarget evt)
            {
                evt.AttackBonusStat = Stat;
            }

            public void OnEventDidTrigger(RuleCalculateAttackBonusWithoutTarget evt)
            {
            }
        }


        [ComponentName("Weapon Damage Stat Replacement")]
        public class WeaponDamageStatReplacement : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RuleCalculateWeaponStats>, IRulebookHandler<RuleCalculateWeaponStats>, IInitiatorRulebookSubscriber
        {
            public StatType Stat;

            public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
            {
                if (this.Owner.Wielder == null || evt.Weapon != this.Owner)
                    return;
                evt.OverrideDamageBonusStat(this.Stat);
            }

            public void OnEventDidTrigger(RuleCalculateWeaponStats evt)
            {
            }
        }


        [ComponentName("change weapon damage")]
        public class WeaponDamageChange : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RuleCalculateWeaponStats>, IRulebookHandler<RuleCalculateWeaponStats>, IInitiatorRulebookSubscriber
        {
            public DiceFormula dice_formula;
            public int bonus_damage;
            public DamageTypeDescription damage_type_description = null;

            public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
            {

                evt.WeaponDamageDiceOverride = dice_formula;
                evt.AddBonusDamage(bonus_damage);
            }

            public void OnEventDidTrigger(RuleCalculateWeaponStats evt)
            {
               if (damage_type_description != null && evt.DamageDescription.Count()>0)
                {
                    evt.DamageDescription[0].TypeDescription = damage_type_description;
                }
            }
        }


        [ComponentName("Remove Weapon Damage Stat")]
        public class Immaterial : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RuleCalculateWeaponStats>, IRulebookHandler<RuleCalculateWeaponStats>, IInitiatorRulebookSubscriber, IInitiatorRulebookHandler<RuleAttackRoll>, IRulebookHandler<RuleAttackRoll>
        {
            public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
            {
                if (this.Owner.Wielder == null || evt.Weapon != this.Owner)
                    return;
                Harmony12.Traverse.Create(evt).Property("DamageBonusStat").SetValue(new StatType?());
                //Helpers.SetField(evt, "DamageBonusStat", new StatType?());
            }

            public void OnEventDidTrigger(RuleCalculateWeaponStats evt)
            {
            }


            public void OnEventAboutToTrigger(RuleAttackRoll evt)
            {

                evt.AttackType = AttackType.Touch;
            }

            public void OnEventDidTrigger(RuleAttackRoll evt)
            {
            }
        }



        [ComponentName("Metamagic effect on weapon damage")]
        public class WeaponMetamagicDamage : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RulePrepareDamage>, IRulebookHandler<RulePrepareDamage>, IInitiatorRulebookSubscriber
        {
            public bool maximize = false;
            public bool empower = false;

            public void OnEventAboutToTrigger(RulePrepareDamage evt)
            {
                if (evt.DamageBundle.Count() == 0)
                {
                    return;
                }

                if (empower)
                {
                    evt.DamageBundle.First().EmpowerBonus = 1.5f;
                }

                if (maximize)
                {
                    evt.DamageBundle.First().Maximized = true;
                }

            }

            public void OnEventDidTrigger(RulePrepareDamage evt)
            {
            }
        }



        public class ConsumeResourceIfAbilitiesFromGroupActivated : ContextAction
        {
            public ActivatableAbilityGroup group;
            public int num_abilities_activated;
            public BlueprintAbilityResource resource;


            public override string GetCaption()
            {
                return $"Consume resource ({resource.Name}) on {num_abilities_activated} from {group.ToString()}";
            }

            public override void RunAction()
            {
                if (resource == null)
                {
                    return;
                }
                var unit = this.Target?.Unit;
                if (unit == null)
                {
                    return;
                }

                if (unit.Descriptor.Resources.GetResourceAmount(resource) <= 0)
                {
                    return;
                }

                int num_activated = 0;
                foreach (var a in unit.ActivatableAbilities)
                {
                    if (a.Blueprint.Group == group && a.IsOn)
                    {
                        num_activated++;
                    }
                }
                if (num_activated >= num_abilities_activated)
                {
                    unit.Descriptor.Resources.Spend((BlueprintScriptableObject)this.resource, 1);
                }
            }
        }


        public class DeactivatedAbilityFromGroup : ContextAction
        {
            public ActivatableAbilityGroup group;
            public int num_abilities_activated;


            public override string GetCaption()
            {
                return $"Deactivated Ability From Group {group.ToString()} if more than {num_abilities_activated}.";
            }

            public override void RunAction()
            {
                var unit = this.Target?.Unit;
                if (unit == null)
                {
                    return;
                }
                int num_activated = 0;
                foreach (var a in unit.ActivatableAbilities)
                {
                    if (a.Blueprint.Group == group && a.IsOn)
                    {
                        if (num_activated < num_abilities_activated)
                        {
                            num_activated++;
                        }
                        else
                        {
                            a.Deactivate();
                        }
                    }
                    
                }
            }
        }


        [AllowedOn(typeof(BlueprintUnitFact))]
        [AllowMultipleComponents]
        public class CoordinatedShotAttackBonus : RuleInitiatorLogicComponent<RuleCalculateAttackBonus>
        {
            public int AttackBonus = 1;
            public int AdditionalFlankBonus = 1;
            public BlueprintUnitFact CoordinatedShotFact;


            public override void OnEventAboutToTrigger(RuleCalculateAttackBonus evt)
            {
                if  (!evt.Weapon.Blueprint.IsRanged)
                    return;

                int bonus = AttackBonus + (evt.Target.CombatState.IsFlanked ? 0 : AdditionalFlankBonus);
                if (this.Owner.State.Features.SoloTactics)
                {
                    evt.AddBonus(bonus, this.Fact);
                    return;
                }

                foreach (UnitEntityData unitEntityData in evt.Target.CombatState.EngagedBy)
                {
                    if (unitEntityData.Descriptor.HasFact(this.CoordinatedShotFact) && unitEntityData != this.Owner.Unit)
                    {
                        evt.AddBonus(bonus, this.Fact);
                        return;
                    }
                }
            }

            public override void OnEventDidTrigger(RuleCalculateAttackBonus evt)
            {
            }
        }


        [ComponentName("Healing bonus")]
        [AllowedOn(typeof(Kingmaker.Blueprints.Facts.BlueprintUnitFact))]
        public class HealingBonusCasterLevel : OwnedGameLogicComponent<UnitDescriptor>, ITargetRulebookSubscriber, ITargetRulebookHandler<RuleCalculateAbilityParams>, ITargetRulebookHandler<RuleHealDamage>,
                                               IRulebookHandler<RuleHealDamage>, IRulebookHandler<RuleCalculateAbilityParams>
        {
            private int bonus = 0;
            public  void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                bonus = 0;
                Main.logger.Log($"triggered + {bonus}");
            }

            public  void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {
                bonus = evt.Result.CasterLevel;
                Main.logger.Log($"triggered + {bonus}");
            }



            public void OnEventAboutToTrigger(RuleHealDamage evt)
            {

            }


            public void OnEventDidTrigger(RuleHealDamage evt)
            {
                Main.logger.Log($"here + {bonus}");
                if (bonus == 0 || evt.Target.Descriptor != this.Owner)
                {
                    bonus = 0;
                    return;
                }
                int old_value = bonus;
                bonus = 0;
                GameHelper.HealDamage(evt.Target, evt.Target, old_value);
                Main.logger.Log($"{this.Fact} restores {old_value} HP");
            }
        }


        [ComponentName("Increase specified spells  DC")]
        [AllowedOn(typeof(BlueprintBuff))]
        public class IncreaseSpecifiedSpellsDC : BuffLogic, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IRulebookHandler<RuleCalculateAbilityParams>, IInitiatorRulebookSubscriber
        {
            public BlueprintAbility[] spells;
            public int BonusDC;

            public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
            {
                if (!spells.Contains(evt.Spell))
                    return;
                evt.AddBonusDC(this.BonusDC);

            }

            public void OnEventDidTrigger(RuleCalculateAbilityParams evt)
            {

            }
        }
    }
}
