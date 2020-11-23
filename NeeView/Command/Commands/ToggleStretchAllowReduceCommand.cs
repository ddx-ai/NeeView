﻿using System;
using System.Windows.Data;


namespace NeeView
{
    public class ToggleStretchAllowReduceCommand : CommandElement
    {
        public ToggleStretchAllowReduceCommand(string name) : base(name)
        {
            this.Group = Properties.Resources.CommandGroupImageScale;
            this.Text = Properties.Resources.CommandToggleStretchAllowReduce;
            this.Note = Properties.Resources.CommandToggleStretchAllowReduceNote;
            this.IsShowMessage = true;
        }

        public override Binding CreateIsCheckedBinding()
        {
            return new Binding(nameof(ViewConfig.AllowStretchScaleDown)) { Source = Config.Current.View };
        }

        public override string ExecuteMessage(object sender, CommandContext e)
        {
            return this.Text + (Config.Current.View.AllowStretchScaleDown ? " OFF" : "");
        }

        public override bool CanExecute(object sender, CommandContext e)
        {
            return !NowLoading.Current.IsDispNowLoading;
        }

        [MethodArgument("@CommandToggleArgument")]
        public override void Execute(object sender, CommandContext e)
        {
            if (e.Args.Length > 0)
            {
                Config.Current.View.AllowStretchScaleDown = Convert.ToBoolean(e.Args[0]);
            }
            else
            {
                Config.Current.View.AllowStretchScaleDown = !Config.Current.View.AllowStretchScaleDown;
            }
        }
    }
}
