﻿using NeeLaboratory.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NeeView
{
    /// <summary>
    /// 画像をストリームで取得
    /// </summary>
    public class PictureStream : BindableBase, IPictureStream
    {
        private readonly DefaultPictureStream _default = new();
        private readonly SusiePictureStream _susie = new();
        private List<IPictureStream> _orderList = new();


        // コンストラクタ
        public PictureStream()
        {
            Config.Current.Susie.AddPropertyChanged(nameof(SusieConfig.IsEnabled),
                (s, e) => UpdateOrderList());
            Config.Current.Susie.AddPropertyChanged(nameof(SusieConfig.IsFirstOrderSusieImage),
                (s, e) => UpdateOrderList());

            UpdateOrderList();
        }


        // 適用する画像ストリームの順番を更新
        private void UpdateOrderList()
        {
            if (!Config.Current.Susie.IsEnabled)
            {
                _orderList = new List<IPictureStream>() { _default };
            }
            else if (Config.Current.Susie.IsFirstOrderSusieImage)
            {
                _orderList = new List<IPictureStream>() { _susie, _default };
            }
            else
            {
                _orderList = new List<IPictureStream>() { _default, _susie };
            }
        }

        // 画像ストリームを取得
        public NamedStream Create(ArchiveEntry entry)
        {
            Exception? exception = null;

            foreach (var pictureStream in _orderList)
            {
                try
                {
                    var stream = pictureStream.Create(entry);
                    if (stream != null) return stream;
                }
                catch (OutOfMemoryException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{e.Message}\nat '{entry.EntryName}' by {pictureStream}");
                    exception = e;
                }
            }

            throw exception ?? new IOException(Properties.Resources.ImageLoadFailedException_Message);
        }
    }

}
