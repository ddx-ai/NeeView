﻿using System;
using System.Windows.Data;


namespace NeeView
{
    public class ToggleIsAutoRotateForcedRightCommand : CommandElement
    {
        public ToggleIsAutoRotateForcedRightCommand()
        {
            this.Group = Properties.Resources.CommandGroup_ViewManipulation;
            this.IsShowMessage = true;
        }

        public override Binding CreateIsCheckedBinding()
        {
            return new Binding(nameof(ViewPropertyControl.IsAutoRotateForcedRight)) { Source = MainViewComponent.Current.ViewPropertyControl };
        }

        public override string ExecuteMessage(object? sender, CommandContext e)
        {
            return MainViewComponent.Current.ViewPropertyControl.GetAutoRotateForcedRight() ? Properties.Resources.ToggleIsAutoRotateForcedRightCommand_Off : Properties.Resources.ToggleIsAutoRotateForcedRightCommand_On;
        }

        public override bool CanExecute(object? sender, CommandContext e)
        {
            return !NowLoading.Current.IsDispNowLoading;
        }

        [MethodArgument("@ToggleCommand.Execute.Remarks")]
        public override void Execute(object? sender, CommandContext e)
        {
            if (e.Args.Length > 0)
            {
                MainViewComponent.Current.ViewPropertyControl.SetAutoRotateForcedRight(Convert.ToBoolean(e.Args[0]));
            }
            else
            {
                MainViewComponent.Current.ViewPropertyControl.ToggleAutoRotateForcedRight();
            }
        }
    }
}
