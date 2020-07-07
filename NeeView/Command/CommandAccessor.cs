﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace NeeView
{
    /// <summary>
    /// コマンドアクセス
    /// </summary>
    public class CommandAccessor
    {
        private CommandElement _command;
        private CommandParameterPropertyMapSource _propertyMapSource;
        private IDictionary<string, object> _patch;

        public CommandAccessor(CommandElement command)
        {
            _command = command;
            _propertyMapSource = new CommandParameterPropertyMapSource(_command);
            Parameter = _propertyMapSource.PropertyMap;
        }

        [WordNodeMember]
        public bool IsShowMessage
        {
            get { return _command.IsShowMessage; }
            set { _command.IsShowMessage = value; }
        }

        [WordNodeMember]
        public string ShortCutKey
        {
            get { return _command.ShortCutKey; }
            set { _command.ShortCutKey = value; }
        }

        [WordNodeMember]
        public string TouchGesture
        {
            get { return _command.TouchGesture; }
            set { _command.TouchGesture = value; }
        }

        [WordNodeMember]
        public string MouseGesture
        {
            get { return _command.MouseGesture; }
            set { _command.MouseGesture = value.Replace("←", "L").Replace("↑", "U").Replace("→", "R").Replace("↓", "L").Replace("Click", "C"); }
        }

        public PropertyMap Parameter { get; }


        [WordNodeMember]
        public bool Execute(params object[] args)
        {
            var parameter = _command.CreateOverwriteCommandParameter(_patch);
            var arguments = args ?? CommandElement.EmptyArgs;
            if (_command.CanExecute(parameter, arguments, CommandOption.None))
            {
                _command.Execute(parameter, arguments, CommandOption.None);
                return true;
            }
            else
            {
                return false;
            }
        }

        [WordNodeMember]
        public CommandAccessor Patch(IDictionary<string, object> patch)
        {
            if (_patch == null)
            {
                _patch = patch;
            }
            else
            {
                foreach (var pair in patch)
                {
                    _patch[pair.Key] = pair.Value;
                }
            }

            return this;
        }

        internal WordNode CreateWordNode(string commandName)
        {
            var node = new WordNode(commandName);
            node.Children = new List<WordNode>();

            var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<WordNodeMemberAttribute>() != null)
                {
                    node.Children.Add(new WordNode(method.Name));
                }
            }

            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.GetCustomAttribute<WordNodeMemberAttribute>() != null)
                {
                    node.Children.Add(new WordNode(property.Name));
                }
            }

            if (Parameter != null)
            {
                node.Children.Add(Parameter.CreateWordNode(nameof(Parameter)));
            }

            return node;
        }

    }

}
