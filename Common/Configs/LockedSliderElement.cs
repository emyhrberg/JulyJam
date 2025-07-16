using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.Social.Steam;
using Terraria.UI;
using static System.Reflection.BindingFlags;

namespace Terrasweeper.Common.Configs;

[Autoload(Side = ModSide.Client)]
public abstract class LockedSliderElement<T> : PrimitiveRangeElement<T>, ILoadable where T : IComparable<T>
{
    #region Private Fields

    private const float TheMagicNumber = 167f;

    private const float LockedBackgroundMultiplier = .4f;

    private static readonly Color LockedGradient = new(40, 40, 40);

    private static ILHook? SkipDrawing;

    private static bool Drawing = false;

    #endregion

    #region Properties

    public object? TargetInstance { get; private set; }

    public PropertyFieldWrapper? TargetMember { get; private set; }

    public bool Mode { get; private set; } = false;

    public bool IsLocked =>
        (bool)(TargetMember?.GetValue(TargetInstance) ?? false) == Mode;

    #endregion

    #region Loading

    public void Load(Mod mod)
    {
        Main.QueueMainThreadAction(() =>
        {
            MethodInfo? drawSelf = typeof(RangeElement).GetMethod("DrawSelf", NonPublic | Instance);

            if (drawSelf is not null)
                SkipDrawing = new(drawSelf,
                    SkipRangeElementDrawing);
        });
    }

    public void Unload() =>
        Main.QueueMainThreadAction(() => SkipDrawing?.Dispose());

    private void SkipRangeElementDrawing(ILContext il)
    {
        try
        {
            ILCursor c = new(il);

            ILLabel jumpret = c.DefineLabel();

            c.GotoNext(MoveType.After,
                i => i.MatchCall<ConfigElement>("DrawSelf"));

            c.EmitDelegate(() => Drawing);
            c.EmitBrfalse(jumpret);

            c.EmitRet();

            c.MarkLabel(jumpret);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to modify IL for {ModContent.GetInstance<Terrasweeper>()}: {e}");
        }
    }

    #endregion

    #region Binding

    public override void OnBind()
    {
        base.OnBind();

        LockedElementAttribute? attri = ConfigManager.GetCustomAttributeFromMemberThenMemberType<LockedElementAttribute>(MemberInfo, Item, List);

        Type? type = attri?.TargetConfig;

        string? name = attri?.MemberName;

        bool? mode = attri?.Mode;

        if (type is null || string.IsNullOrEmpty(name) || mode is null)
            return;

        FieldInfo? field = type.GetField(name, Static | Instance | Public | NonPublic);
        PropertyInfo? property = type.GetProperty(name, Static | Instance | Public | NonPublic);

        if (field is not null)
            TargetMember = new(field);
        else
            TargetMember = new(property);

        if (ConfigManager.Configs.TryGetValue(ModContent.GetInstance<Terrasweeper>(), out List<ModConfig>? value))
            TargetInstance = value.Find(c => c.Name == type.Name);
        else
            TargetInstance = null;

        Mode = mode ?? false;
    }

    #endregion

    #region Drawing

    public override void DrawSelf(SpriteBatch spriteBatch)
    {
        // I genuinely hate that what I'd indeally want is ONLY possible in IL.
        Drawing = true;
        backgroundColor = IsLocked ? UICommon.DefaultUIBlue * LockedBackgroundMultiplier : UICommon.DefaultUIBlue;
        base.DrawSelf(spriteBatch);
        Drawing = false;

        rightHover = null;

        if (!Main.mouseLeft)
            rightLock = null;

        CalculatedStyle dimensions = GetDimensions();

        // Not sure the purpose of this.
        IngameOptions.valuePosition = new(dimensions.X + dimensions.Width - 10f, dimensions.Y + 16f);

        DrawSlider(spriteBatch, Proportion, out float ratio);

        // No need to run logic if the value doesn't do anything.
        if (IsLocked)
            return;

        if (IngameOptions.inBar || rightLock == this)
        {
            rightHover = this;
            if (PlayerInput.Triggers.Current.MouseLeft && rightLock == this)
                Proportion = ratio;
        }

        if (rightHover is not null && rightLock is null && PlayerInput.Triggers.JustPressed.MouseLeft)
            rightLock = rightHover;
    }

    public void DrawSlider(SpriteBatch spriteBatch, float perc, out float ratio)
    {
        perc = MathHelper.Clamp(perc, -0.05f, 1.05f);

        Texture2D colorBar = TextureAssets.ColorBar.Value;
        Texture2D colorBarHighlight = TextureAssets.ColorHighlight.Value;
        Texture2D gradient = Ass.Gradient.Value;
        Texture2D colorSlider = TextureAssets.ColorSlider.Value;
        Texture2D lockIcon = Ass.Lock.Value;

        IngameOptions.valuePosition.X -= colorBar.Width;
        Rectangle rectangle = new((int)IngameOptions.valuePosition.X, (int)IngameOptions.valuePosition.Y - (int)(colorBar.Height * .5f), colorBar.Width, colorBar.Height);
        Rectangle destinationRectangle = rectangle;

        float x = rectangle.X + 5f;
        float y = rectangle.Y + 4f;

        spriteBatch.Draw(colorBar, rectangle, IsLocked ? Color.Gray : Color.White);

        Rectangle inner = new((int)x, (int)y, (int)TheMagicNumber + 2, 8);

        // Draw the gradient
        spriteBatch.Draw(gradient, inner, null, IsLocked ? LockedGradient : SliderColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);

        rectangle.Inflate(-5, 2);

        // Logic.
        bool isHovering = rectangle.Contains(new Point(Main.mouseX, Main.mouseY)) || rightLock == this;

        if (rightLock != this && rightLock is not null || IsLocked)
            isHovering = false;

        if (isHovering)
            spriteBatch.Draw(colorBarHighlight, destinationRectangle, Main.OurFavoriteColor);

        Vector2 lockOffset = new(0, -4);

        if (IsLocked)
            spriteBatch.Draw(lockIcon, inner.Center() + lockOffset, null, Color.White, 0f, lockIcon.Size() * .5f, 1f, SpriteEffects.None, 0f);
        else
            spriteBatch.Draw(colorSlider, new Vector2(x + TheMagicNumber * perc, y + 4f), null, Color.White, 0f, colorSlider.Size() * .5f, 1f, SpriteEffects.None, 0f);

        IngameOptions.inBar = isHovering;
        ratio = MathHelper.Clamp((Main.mouseX - rectangle.X) / (float)rectangle.Width, 0f, 1f);
    }

    #endregion
}