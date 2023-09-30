﻿namespace NeeView
{
    public class TogglePageModeCommand : CommandElement
    {
        public const string DefaultMouseGesture = "RD";

        public TogglePageModeCommand()
        {
            this.Group = Properties.Resources.CommandGroup_PageSetting;
            this.MouseGesture = DefaultMouseGesture;
            this.IsShowMessage = true;
        }

        public override string ExecuteMessage(object? sender, CommandContext e)
        {
            return BookSettingPresenter.Current.LatestSetting.PageMode.GetToggle(+1).ToAliasName();
        }

        public override void Execute(object? sender, CommandContext e)
        {
            BookSettingPresenter.Current.TogglePageMode(+1);
        }
    }
}
