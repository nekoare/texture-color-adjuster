using nadena.dev.ndmf;
using TexColAdjuster.Runtime;

[assembly: ExportsPlugin(typeof(TexColAdjuster.Editor.NDMF.TexColorAdjusterPlugin))]

namespace TexColAdjuster.Editor.NDMF
{
    public class TexColorAdjusterPlugin : Plugin<TexColorAdjusterPlugin>
    {
        public override string DisplayName => "Texture Color Adjuster";
        public override string QualifiedName => "dev.nekoare.tex-col-adjuster";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run(TextureAdjustmentPass.Instance)
                .PreviewingWith(new TexColorAdjusterPreview());

            InPhase(BuildPhase.Optimizing)
                .Run(RemoveTexColComponentsPass.Instance);
        }
    }
}
