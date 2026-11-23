// Exibe a tecla atual de cada ação, respeitando remapeamentos.

using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    public enum TutorialGlyph
    {
        Key,
        LeftMouse,
        RightMouse,
        MiddleMouse,
        Wheel,
        Move
    }

    public readonly struct TutorialBinding
    {
        public readonly string text;
        public readonly TutorialGlyph glyph;
        public TutorialBinding(string value, TutorialGlyph shape = TutorialGlyph.Key)
        {
            text = value;
            glyph = shape;
        }
    }

    public static class TutorialBindings
    {
        public static TutorialBinding Resolve(InputActionAsset asset, string id, PhoneMessages phone = null)
        {
            // The existing minigame reads mouse/Esc directly; equipment rebinding must not mislabel its gestures.
            if (id == "LockpickTension")
                return new TutorialBinding("", TutorialGlyph.LeftMouse);
            if (id == "LockpickCancel")
                return new TutorialBinding("Esc");
            if (id == "Phone")
                return new TutorialBinding(phone ? phone.openKey.ToString() : "P");
            if (id == "Slots")
            {
                string result = "";
                for (int i = 1; i <= 3; i++)
                {
                    if (i > 1)
                        result += " / ";
                    result += Resolve(asset, "Equip" + i).text;
                }

                return new TutorialBinding(result);
            }

            if (id == "MouseMove")
                return new TutorialBinding("MOVER", TutorialGlyph.Move);
            var action = asset ? asset.FindAction(id, false) : null;
            if (action == null && asset)
                action = asset.FindAction("Gameplay/" + id, false);
            if (action != null)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    string path = b.effectivePath ?? "";
                    if (b.isComposite || (!path.StartsWith("<Keyboard>") && !path.StartsWith("<Mouse>")))
                        continue;
                    if (path.StartsWith("<Mouse>"))
                    {
                        var glyph = path.Contains("leftButton") ? TutorialGlyph.LeftMouse : path.Contains("rightButton") ? TutorialGlyph.RightMouse : path.Contains("middleButton") ? TutorialGlyph.MiddleMouse : path.Contains("scroll") ? TutorialGlyph.Wheel : TutorialGlyph.Move;
                        return new TutorialBinding(glyph == TutorialGlyph.Wheel ? "RODA" : glyph == TutorialGlyph.Move ? "MOVER" : "", glyph);
                    }

                    return new TutorialBinding(action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions));
                }

            return new TutorialBinding(id ?? "Ação");
        }
    }
}
