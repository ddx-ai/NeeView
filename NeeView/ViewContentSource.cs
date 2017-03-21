﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NeeView
{
    // 表示コンテンツソース 1ページ分
    public class ViewContentSource
    {
        // ページ
        // TODO: 不要にしたいが。難しいか。
        public Page Page { get; set; }


        // ソースコンテンツ
        public PageContent Content { get; set; }


        // コンテンツサイズ 
        public Size Size { get; set; }


        // ページの場所
        public PagePosition Position { get; set; }

        // ページサイズ
        public int PartSize { get; set; }

        // 方向
        public PageReadOrder ReadOrder { get; set; }

        // 有効
        public bool IsValid { get; set; }

        // コンストラクタ
        // Pageから作成
        public ViewContentSource(Page page, PagePosition position, int size, PageReadOrder readOrder)
        {
            Page = page;
            Content = page.Content;

            IsValid = Content.IsLoaded;

            Size = new Size(size == 2 ? Content.Size.Width : Math.Floor(Content.Size.Width * 0.5 + 0.4), Content.Size.Height);

            Position = position;
            PartSize = size;
            ReadOrder = readOrder;
        }

        /// <summary>
        /// ViewBox取得.
        /// ページ分割対応.
        /// ポリゴン表示誤吸収のための補正付き
        /// </summary>
        /// <returns></returns>
        private Rect GetViewBox()
        {
            if (PartSize == 0) return new Rect(0, -0.00001, 0, 0.99999);
            if (PartSize == 2) return new Rect(-0.00001, -0.00001, 0.99999, 0.99999);

            bool isRightPart = Position.Part == 0;
            if (ReadOrder == PageReadOrder.LeftToRight) isRightPart = !isRightPart;

            double half = Size.Width / Content.Size.Width;
            return isRightPart ? new Rect(0.99999 - half, -0.00001, half - 0.00001, 0.99999) : new Rect(-0.00001, -0.00001, half - 0.00001, 0.99999);
        }


        /// <summary>
        /// ページ用画像ブラシ作成
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private ImageBrush CreatePageImageBrush(BitmapSource bitmap)
        {
            var brush = new ImageBrush();
            brush.ImageSource = bitmap;

            brush.AlignmentX = AlignmentX.Left;
            brush.AlignmentY = AlignmentY.Top;
            brush.Stretch = Stretch.Fill;
            brush.TileMode = TileMode.None;
            brush.Viewbox = GetViewBox();

            return brush;
        }

        // コントロール作成
        public FrameworkElement CreateControl(Binding foregroundBinding, Binding bitmapScalingModeBinding, ViewContentReserver reserver)
        {
            // テキスト表示
            if (Content.PageMessage != null)
            {
                var filepage = new FilePageContent()
                {
                    Icon = Content.PageMessage.Icon,
                    FileName = Content.Entry.EntryName,
                    Message = Content.PageMessage.Message,
                };

                var control = new FilePageControl(filepage);
                control.SetBinding(FilePageControl.DefaultBrushProperty, foregroundBinding);
                return control;
            }
            // 仮表示
            else if (!Content.IsLoaded)
            {
                var rectangle = new Rectangle();
                rectangle.Fill = CreateThumbnailBrush(reserver);
                RenderOptions.SetBitmapScalingMode(rectangle, BitmapScalingMode.HighQuality);
                return rectangle;
            }

            //
            else if (Content is AnimatedContent)
            {
                var media = new MediaElement();
                media.Source = new Uri(((AnimatedContent)Content).FileProxy.Path);
                media.MediaEnded += (s, e_) => media.Position = TimeSpan.FromMilliseconds(1);
                media.MediaFailed += (s, e_) => { throw new ApplicationException("MediaElementで致命的エラー", e_.ErrorException); };
                media.SetBinding(RenderOptions.BitmapScalingModeProperty, bitmapScalingModeBinding);

                var brush = new VisualBrush();
                brush.Visual = media;
                brush.Stretch = Stretch.Fill;
                brush.Viewbox = GetViewBox();

                var rectangle = new Rectangle();
                rectangle.Fill = brush;
                return rectangle;
            }
            else if (Content is BitmapContent)
            {
                var rectangle = new Rectangle();
                rectangle.Fill = CreatePageImageBrush(((BitmapContent)Content).BitmapSource);
                rectangle.SetBinding(RenderOptions.BitmapScalingModeProperty, bitmapScalingModeBinding);
                rectangle.UseLayoutRounding = true;
                rectangle.SnapsToDevicePixels = true;
                return rectangle;
            }
            else
            {
                var rectangle = new Rectangle();
                rectangle.Fill = CreateThumbnailBrush(null);
                RenderOptions.SetBitmapScalingMode(rectangle, BitmapScalingMode.HighQuality);
                return rectangle;
            }
        }

        /// <summary>
        /// サムネイル作成
        /// </summary>
        /// <returns></returns>
        public Brush CreateThumbnailBrush(ViewContentReserver reserver)
        {
            if (Page.Thumbnail.IsValid)
            {
                return CreatePageImageBrush(Page.Thumbnail.CreateBitmap());
            }
            else if (reserver?.Thumbnail != null && reserver.Thumbnail.IsValid)
            {
                return CreatePageImageBrush(reserver.Thumbnail.CreateBitmap());
            }
            else
            {
                return new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
            }
        }

        /// <summary>
        /// PageContent作成
        /// </summary>
        /// <param name="foregroundBinding"></param>
        /// <param name="bitmapScalingModeBinding"></param>
        /// <returns></returns>
        public PageContentView CreatePageContent(Binding foregroundBinding, Binding bitmapScalingModeBinding, ViewContentReserver reserver)
        {
            var element = CreateControl(foregroundBinding, bitmapScalingModeBinding, reserver);

            var textblock = new TextBlock();
            textblock.Text = LoosePath.GetFileName(this.Page.FullPath); // Position.ToString();
            textblock.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            textblock.FontSize = 20;
            textblock.Margin = new Thickness(10);
            textblock.HorizontalAlignment = HorizontalAlignment.Center;
            textblock.VerticalAlignment = VerticalAlignment.Center;

            var pageContent = new PageContentView(element, textblock);
            pageContent.IsHitTestVisible = false;
            return pageContent;
        }
    }


    // 表示コンテンツソースの種類
    public enum ViewSourceType
    {
        None, // 無し
        Content, // コンテンツ
    }

    // 表示コンテンツソース
    public class ViewSource
    {
        public ViewSourceType Type { get; set; }
        public List<ViewContentSource> Sources { get; set; }
        public int Direction { get; set; }
    }
}
