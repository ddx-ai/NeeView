﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NeeView
{
    public class CommandAccessorMap : Dictionary<string, ICommandAccessor>
    {
        public CommandAccessorMap(object sender, CommandTable commandTable, IAccessDiagnostics accessDiagnostics)
        {
            if (accessDiagnostics is null)
            {
                throw new ArgumentNullException(nameof(accessDiagnostics));
            }

            foreach (var item in commandTable)
            {
                this.Add(item.Key, new CommandAccessor(sender, item.Value, accessDiagnostics));
            }

#pragma warning disable CS0612 // 型またはメンバーが旧型式です
            foreach (var item in commandTable.ObsoleteCommands)
            {
                this.Add(item.Key, new ObsoleteCommandAccessor(item.Key, item.Value, accessDiagnostics));
            }
#pragma warning restore CS0612 // 型またはメンバーが旧型式です
        }

        internal WordNode CreateWordNode(string name)
        {
            var node = new WordNode(name);
            node.Children = new List<WordNode>();
            foreach (var commandName in this.Keys)
            {
                var commandAccessor = this[commandName] as CommandAccessor;
                if (commandAccessor != null)
                {
                    node.Children.Add(commandAccessor.CreateWordNode(commandName));
                }
            }
            return node;
        }
    }
}
