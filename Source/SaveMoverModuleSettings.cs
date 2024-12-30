using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SaveMover;

public class SaveMoverModuleSettings : EverestModuleSettings {

    [DefaultButtonBinding(button: Buttons.LeftTrigger, key: Keys.Tab)]
    public ButtonBinding SaveMoverBind { get; set; }

}