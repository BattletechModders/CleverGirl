
namespace CleverGirl {

    public class ModConfig {

        public bool Debug = false;
        public bool Trace = false;

        public override string ToString() {
            return $"Debug:{Debug}";
        }
    }
}
