﻿using System.Windows.Data;


namespace NeeView
{
    public class ToggleVisibleTitleBarCommand : CommandElement
    {
        public ToggleVisibleTitleBarCommand(string name) : base(name)
        {
            this.Group = Properties.Resources.CommandGroupWindow;
            this.Text = Properties.Resources.CommandToggleVisibleTitleBar;
            this.MenuText = Properties.Resources.CommandToggleVisibleTitleBarMenu;
            this.Note = Properties.Resources.CommandToggleVisibleTitleBarNote;
            this.IsShowMessage = false;
        }

        public override Binding CreateIsCheckedBinding()
        {
            return new Binding(nameof(WindowConfig.IsCaptionVisible)) { Source = Config.Current.Window, Mode = BindingMode.OneWay };
        }

        public override string ExecuteMessage(object sender, CommandContext e)
        {
            return Config.Current.Window.IsCaptionVisible ? Properties.Resources.CommandToggleVisibleTitleBarOff : Properties.Resources.CommandToggleVisibleTitleBarOn;
        }

        public override void Execute(object sender, CommandContext e)
        {
            WindowShape.Current.ToggleCaptionVisible();
        }
    }
}
