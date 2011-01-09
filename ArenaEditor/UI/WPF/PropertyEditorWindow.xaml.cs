﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AW2.UI.WPF
{
    /// <summary>
    /// Interaction logic for PropertyEditorWindow.xaml
    /// </summary>
    public partial class PropertyEditorWindow : Window
    {
        public object SelectedObject
        {
            get { return EditorGrid.SelectedObject; }
            set
            {
                EditorGrid.SelectedObject = value;
                if (value != null && !IsVisible) Show();
            }
        }

        public PropertyEditorWindow()
        {
            InitializeComponent();
        }
    }
}