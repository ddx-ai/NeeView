﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NeeView
{
    // ドラッグアクションの種類
    public enum DragActionType
    {
        None,
        Move,
        MoveScale,
        Angle,
        Scale,
        ScaleSlider,
        FlipHorizontal,
        FlipVertical,
        WindowMove,
    }

    // ドラッグアクショングループ
    public enum DragActionGroup
    {
        None, // どのグループにも属さない
        Move,
    };

    
    // ドラッグアクション
    public class DragAction
    {
        public string Name;
        public string Key = "";
        public Action<Point, Point> Exec;
        public DragActionGroup Group;

        // グループ判定
        public bool IsGroupCompatible(DragAction target)
        {
            return Group != DragActionGroup.None && Group == target.Group;
        }

        // キー を DragKey のコレクションに変換
        public List<DragKey> GetDragKeyCollection()
        {
            var list = new List<DragKey>();
            if (Key != null)
            {
                var converter = new DragKeyConverter();
                foreach (var key in Key.Split(','))
                {
                    try
                    {
                        var dragKey = converter.ConvertFromString(key);
                        list.Add(dragKey);
                    }
                    catch { }
                }
            }

            return list;
        }


        #region Memento

        [DataContract]
        public class Memento
        {
            [DataMember]
            public string Key { get; set; }

            //
            private void Constructor()
            {
                Key = "";
            }

            //
            public Memento()
            {
                Constructor();
            }

            //
            [OnDeserializing]
            private void Deserializing(StreamingContext c)
            {
                Constructor();
            }

            //
            public Memento Clone()
            {
                return (Memento)MemberwiseClone();
            }
        }

        //
        public Memento CreateMemento()
        {
            var memento = new Memento();
            memento.Key = Key;
            return memento;
        }

        //
        public void Restore(Memento element)
        {
            Key = element.Key;
        }

        #endregion
    }


    // ドラッグキー
    public class DragKey : IEquatable<DragKey>
    {
        public MouseButton MouseButton;
        public ModifierKeys ModifierKeys;

        public DragKey(MouseButton button, ModifierKeys modifiers)
        {
            MouseButton = button;
            ModifierKeys = modifiers;
        }

        public override bool Equals(System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Point return false.
            DragKey p = obj as DragKey;
            if ((System.Object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (MouseButton == p.MouseButton) && (ModifierKeys == p.ModifierKeys);
        }

        public bool Equals(DragKey p)
        {
            // If parameter is null return false:
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (MouseButton == p.MouseButton) && (ModifierKeys == p.ModifierKeys);
        }

        public override int GetHashCode()
        {
            return MouseButton.GetHashCode() ^ ModifierKeys.GetHashCode();
        }

        public static bool operator ==(DragKey a, DragKey b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            // Return true if the fields match:
            return (a.MouseButton == b.MouseButton) && (a.ModifierKeys == b.ModifierKeys);
        }

        public static bool operator !=(DragKey a, DragKey b)
        {
            return !(a == b);
        }
    }


    /// <summary>
    /// マウスドラッグ コンバータ
    /// </summary>
    public class DragKeyConverter
    {
        /// <summary>
        ///  文字列からマウスドラッグアクションに変換する
        /// </summary>
        /// <param name="source">ジェスチャ文字列</param>
        /// <returns>DragKey。変換に失敗したときは NotSupportedException 例外が発生</returns>
        public DragKey ConvertFromString(string source)
        {
            var keys = source.Split('+');

            MouseButton action;
            ModifierKeys modifierKeys = ModifierKeys.None;

            var button = keys.Last();
            if (!button.EndsWith("Drag"))
            {
                throw new NotSupportedException($"'{source}' キーと修飾キーの組み合わせは、DragKey ではサポートされていません。");
            }

            button = button.Substring(0, button.Length - "Drag".Length);
            if (!Enum.TryParse(button, out action))
            {
                throw new NotSupportedException($"'{source}' キーと修飾キーの組み合わせは、DragKey ではサポートされていません。");
            }

            for (int i = 0; i < keys.Length - 1; ++i)
            {
                var key = keys[i];
                if (key == "Ctrl") key = "Control";

                ModifierKeys modifierKeysOne;
                if (Enum.TryParse<ModifierKeys>(key, out modifierKeysOne))
                {
                    modifierKeys |= modifierKeysOne;
                    continue;
                }

                throw new NotSupportedException($"'{source}' キーと修飾キーの組み合わせは、DragKey ではサポートされていません。");
            }

            return new DragKey(action, modifierKeys);
        }

        /// <summary>
        ///  マウスドラッグアクションから文字列に変換する
        /// </summary>
        public string ConvertToString(DragKey gesture)
        {
            string text = "";

            foreach (ModifierKeys key in Enum.GetValues(typeof(ModifierKeys)))
            {
                if ((gesture.ModifierKeys & key) != ModifierKeys.None)
                {
                    text += "+" + ((key == ModifierKeys.Control) ? "Ctrl" : key.ToString());
                }
            }

            text += "+" + gesture.MouseButton + "Drag";

            return text.TrimStart('+');
        }
    }

}
