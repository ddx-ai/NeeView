﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using NeeView.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;

namespace NeeView
{
    //
    public class PictureProfile : BindableBase
    {
        // 
        public static PictureProfile Current { get; private set; }

        #region Fields

        // 有効ファイル拡張子
        private PictureFileExtension _fileExtension = new PictureFileExtension();

        #endregion

        #region Properties

        // 画像最大サイズ
        public Size Maximum { get; set; } = new Size(4096, 4096);

        #endregion

        #region Constructors

        //
        public PictureProfile()
        {
            Current = this;
        }

        #endregion

        #region Methods

        // 対応拡張子判定
        public bool IsSupported(string fileName)
        {
            return _fileExtension.IsSupported(fileName);
        }

        // 最大サイズ内におさまるサイズを返す
        public Size CreateFixedSize(Size size)
        {
            if (size.IsEmpty) return size;

            return size.Limit(this.Maximum);
        }

        #endregion

        #region Memento

        [DataContract]
        public class Memento
        {
            [DataMember]
            public Size Maximum { get; set; }
        }

        //
        public Memento CreateMemento()
        {
            var memento = new Memento();
            memento.Maximum = this.Maximum;
            return memento;
        }

        //
        public void Restore(Memento memento)
        {
            if (memento == null) return;
            this.Maximum = memento.Maximum;
        }
        #endregion

    }
}
