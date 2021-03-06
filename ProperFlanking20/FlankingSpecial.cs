﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;

namespace ProperFlanking20.FlankingSpecial
{
    class GangUp : Flanking.SpecialFlanking
    {
        public int min_additional_flankers = 2;

        public override bool isFlanking(UnitEntityData target)
        {         
            var engaged_array = target.CombatState.EngagedBy.ToArray();

            if (!engaged_array.Contains(this.Owner.Unit) || engaged_array.Length < min_additional_flankers + 1)
            {
                return false;
            }

            var need_flankers = min_additional_flankers;

            foreach (var teammate  in engaged_array)
            {
                if (teammate == this.Owner.Unit)
                {
                    continue;
                }

                need_flankers--;

                if (need_flankers <= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }



    class PackFlanking : Flanking.SpecialFlanking
    {
        public int radius = 5;
        public override bool isFlanking(UnitEntityData target)
        {
            bool solo_tactics = (bool)this.Owner.State.Features.SoloTactics;

            if (!(this.Owner.Unit.Descriptor.IsPet || (this.Owner.Unit.Descriptor.Pet != null)))
            {
                return false;
            }

            var teammate = this.Owner.Unit.Descriptor.IsPet ? this.Owner.Unit.Descriptor.Master.Value : this.Owner.Unit.Descriptor.Pet;
            if (!GameHelper.IsUnitInRange(teammate, this.Owner.Unit.Position, radius, false))
            {
                return false;
            }

            return teammate.CombatState.IsEngage(target) && (teammate.Ensure<Flanking.UnitPartSpecialFlanking>().hasBuff(this.Fact.Blueprint) || solo_tactics);
        }
    }
}
