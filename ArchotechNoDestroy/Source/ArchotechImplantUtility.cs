using System;
using System.Collections.Generic;
using Verse;

namespace ArchotechNoDestroy
{
    internal static class ArchotechImplantUtility
    {
        // vanilla ships ArchotechArm, ArchotechEye and ArchotechLeg, and every other mod invents
        // its own, so match on the name rather than keeping a list that is wrong by next week
        internal static bool IsArchotech(HediffDef def)
        {
            if (def.defName.IndexOf("archo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return def.label != null && def.label.IndexOf("archo", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // true when this exact part is carrying an archotech implant. Hediff_AddedPart inherits
        // Hediff_Implant, so both shapes fall out of the same check
        internal static bool HasArchotechImplantOn(HediffSet hediffSet, BodyPartRecord part)
        {
            List<Hediff> hediffs = hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part == part && hediff is Hediff_Implant && IsArchotech(hediff.def))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
