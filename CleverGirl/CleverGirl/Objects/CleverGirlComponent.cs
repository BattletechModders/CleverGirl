using CustomComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleverGirl.Components {

    [CustomComponent("CleverGirl")]
    public class CleverGirlComponent : SimpleCustomComponent {
        public float ArmorDamageReduction = 0f;
    }
}
