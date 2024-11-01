﻿using System;
using System.Globalization;
using System.Windows.Data;


namespace NeeView
{
    public class ToggleIsAutoRotateLeftCommand : CommandElement
    {
        public ToggleIsAutoRotateLeftCommand()
        {
            this.Group = Properties.TextResources.GetString("CommandGroup.ViewManipulation");
            this.IsShowMessage = true;
        }

        public override Binding CreateIsCheckedBinding()
        {
            return new Binding(nameof(ViewPropertyControl.IsAutoRotateLeft)) { Source = MainViewComponent.Current.ViewPropertyControl };
        }

        public override string ExecuteMessage(object? sender, CommandContext e)
        {
            return MainViewComponent.Current.ViewPropertyControl.GetAutoRotateLeft() ? Properties.TextResources.GetString("ToggleIsAutoRotateLeftCommand.Off") : Properties.TextResources.GetString("ToggleIsAutoRotateLeftCommand.On");
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
                MainViewComponent.Current.ViewPropertyControl.SetAutoRotateLeft(Convert.ToBoolean(e.Args[0], CultureInfo.InvariantCulture));
            }
            else
            {
                MainViewComponent.Current.ViewPropertyControl.ToggleAutoRotateLeft();
            }
        }
    }
}
