using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terrasweeper.Common.BuilderToggles
{
    public class ShowMinesBuilderToggle : BuilderToggle
    {
        public override bool Active() => false; // false = disabled, true = enabled

        public override int NumberOfStates => 2;

        public override void SetStaticDefaults()
        {
            //CurrentState = 1; // ??
        }

        public override string DisplayValue()
        {
            if (CurrentState == 0)
                return "Show Mines On";
            else
                return "Show Mines Off";
        }

        public override void OnRightClick()
        {
            CurrentState -= 1;
            if (CurrentState < 0)
            {
                CurrentState = NumberOfStates - 1;
            }

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override bool Draw(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams)
        {
            drawParams.Frame = drawParams.Texture.Frame(1, 2, 0, CurrentState);
            return base.Draw(spriteBatch, ref drawParams);
        }
    }
}