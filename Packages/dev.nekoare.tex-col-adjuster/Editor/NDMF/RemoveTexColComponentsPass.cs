using nadena.dev.ndmf;
using TexColAdjuster.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TexColAdjuster.Editor.NDMF
{
    internal class RemoveTexColComponentsPass : Pass<RemoveTexColComponentsPass>
    {
        protected override void Execute(BuildContext context)
        {
            if (context.AvatarRootObject == null)
            {
                return;
            }

            var components = context.AvatarRootObject.GetComponentsInChildren<TextureColorAdjustmentComponent>(true);
            if (components == null || components.Length == 0)
            {
                return;
            }

            foreach (var component in components)
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
