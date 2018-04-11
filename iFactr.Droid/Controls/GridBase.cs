using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using iFactr.UI;
using iFactr.UI.Controls;
using Size = iFactr.UI.Size;
using View = Android.Views.View;
using MonoCross.Utilities;

namespace iFactr.Droid
{
    public class GridBase : RadioGroup, IGridBase, IPairable, INotifyPropertyChanged
    {
        #region Constructors

        [Preserve]
        public GridBase()
            : base(DroidFactory.MainActivity)
        {
            Initialize();
        }

        public GridBase(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        public GridBase(Context context)
            : base(context)
        {
            Initialize();
        }

        [Preserve]
        public GridBase(Context context, IAttributeSet attrs)
            : base(context, attrs)
        {
            Initialize(attrs);
        }

        private void Initialize(IAttributeSet attrs = null)
        {
            MaxHeight = double.PositiveInfinity;
            Columns = new ColumnCollection();
            Rows = new RowCollection();

            ChildViewAdded -= GridBase_ChildViewAdded;
            ChildViewAdded += GridBase_ChildViewAdded;
            ChildViewRemoved -= GridBase_ChildViewRemoved;
            ChildViewRemoved += GridBase_ChildViewRemoved;

            if (attrs == null) return;
            var rowCount = attrs.GetAttributeIntValue("http://schemas.android.com/apk/lib/ifactr", "rows", 0);
            if (rowCount > 0) this.SetRows(rowCount);
            var columnCount = attrs.GetAttributeIntValue("http://schemas.android.com/apk/lib/ifactr", "columns", 0);
            if (columnCount > 0) this.SetColumns(columnCount);
            string padding = attrs.GetAttributeValue("http://schemas.android.com/apk/lib/ifactr", "padding");
            if (!string.IsNullOrEmpty(padding))
            {
                var padValues = padding.Split(',').Select(p => p.TryParseDouble()).ToList();
                switch (padValues.Count)
                {
                    case 1:
                        Padding = new Thickness(padValues[0]);
                        break;
                    case 2:
                        Padding = new Thickness(padValues[0], padValues[1]);
                        break;
                    case 4:
                        Padding = new Thickness(padValues[0], padValues[1], padValues[2], padValues[3]);
                        break;
                    default:
                        throw new FormatException("Invalid padding format: " + padding);
                }
            }
        }

        #endregion

        private void GridBase_ChildViewAdded(object sender, ChildViewAddedEventArgs e)
        {
            ResizeRequested = true;

            var control = e.Child as IElement;
            if (control == null) return;

            // Supplying the parent metadata ensures that if the element is removed (via cell recycling
            // for example), it has a local handle to reattach itself.
            control.Metadata["Parent"] = Pair;
            ((IPairable)(control as GridBase))?.SetBinding(new Binding(nameof(Parent), nameof(Parent)) { Source = this, });

            var submission = control as IControl;
            if (submission != null) SetSubmission(submission.SubmitKey, submission.StringValue);

            OnPropertyChanged(nameof(Children));
        }

        private void GridBase_ChildViewRemoved(object sender, ChildViewRemovedEventArgs e)
        {
            ResizeRequested = true;
            var control = e.Child as IElement;
            if (control == null) return;

            var submission = control as IControl;
            if (submission?.SubmitKey != null) GetSubmissions()?.Remove(submission.SubmitKey);
            OnPropertyChanged(nameof(Children));
        }

        public double MaxHeight
        {
            get { return _maxHeight; }
            set
            {
                if (_maxHeight == value) return;
                _maxHeight = value;
                if (_minHeight > _maxHeight)
                {
                    MinHeight = _maxHeight;
                }
                OnPropertyChanged();
            }
        }
        private double _maxHeight;

        public double MinHeight
        {
            get { return _minHeight; }
            set
            {
                if (_minHeight == value) return;
                _minHeight = value;
                if (_maxHeight < _minHeight)
                {
                    MaxHeight = _minHeight;
                }
                OnPropertyChanged();
            }
        }
        private double _minHeight;

        public double MaxWidth
        {
            get { return _maxWidth; }
            set
            {
                if (_maxWidth == value) return;
                _maxWidth = value;
                OnPropertyChanged();
            }
        }
        private double _maxWidth;

        public double MinWidth
        {
            get { return _minWidth; }
            set
            {
                if (_minWidth == value) return;
                _minWidth = value;
                OnPropertyChanged();
            }
        }
        private double _minWidth;

        public new IView Parent
        {
            get { return _parent; }
            set
            {
                if ((value is IGridView || value is IListView) && !Equals(_parent, value))
                {
                    _parent = value;
                    OnPropertyChanged(nameof(Parent));
                }
                if (_parent == null)
                    return;
                var submit = GetSubmissions();
                foreach (var control in Children.OfType<IControl>().Where(control => control.SubmitKey != null && !submit.ContainsKey(control.SubmitKey)))
                {
                    SetSubmission(control.SubmitKey, control.StringValue);
                }
            }
        }
        private IView _parent;

        #region ViewGroup overrides

        /// <summary>
        /// Measure the view and its content to determine the measured width and the measured height.
        /// This method is invoked by <see cref="View.Measure"/> and should be overriden by subclasses to provide accurate and efficient measurement of their contents. 
        /// </summary>
        /// <param name="widthMeasureSpec">Horizontal space requirements as imposed by the parent. The requirements are encoded with <see cref="View.MeasureSpec"/>.</param>
        /// <param name="heightMeasureSpec">Vertical space requirements as imposed by the parent. The requirements are encoded with <see cref="View.MeasureSpec"/>.</param>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            int parentHeight;
            int parentWidth;
            if (Parent == null) return;
            var basicGrid = !(this is GridCell) && !(this is Grid);
            var fragment = (BaseFragment)Parent;
            var frame = DroidFactory.MainActivity.FindViewById<FrameLayout>(((FragmentHistoryStack)fragment.Stack).FragmentId);
            if (frame != null)
            {
                parentWidth = frame.MeasuredWidth;
                parentHeight = (int)(frame.MeasuredHeight / DroidFactory.DisplayScale);
            }
            else
            {
                var metrics = new DisplayMetrics();
                DroidFactory.MainActivity.WindowManager.DefaultDisplay.GetMetrics(metrics);
                parentWidth = (int)(metrics.WidthPixels * .6);
                parentHeight = (int)(parentWidth * .75);
            }

            if (MinWidth < 1) { MinWidth = parentWidth; }
            if (MaxWidth < MinWidth) { MaxWidth = MinWidth; }
            if (basicGrid && MinHeight < 1 && parentHeight > 0) { MinHeight = parentHeight; }
            if (MaxHeight < 1 && parentHeight > 0) { MaxHeight = basicGrid ? parentHeight : double.PositiveInfinity; }

            var size = new Size(MeasuredWidth, MeasuredHeight);
            if (MinWidth < 0 || MinHeight < 0 || MaxWidth < 0 || MaxHeight < 0)
            {
                SetMeasuredDimension(ResolveSize((int)size.Width, widthMeasureSpec), ResolveSize((int)size.Height, heightMeasureSpec));
                return;
            }

            if (ResizeRequested ||
                size.Width < MinWidth || size.Height < MinHeight * DroidFactory.DisplayScale ||
                size.Width > MaxWidth || size.Height > MaxHeight * DroidFactory.DisplayScale)
            {
                var minSize = new Size(Math.Min(MaxWidth, MinWidth), Math.Min(MinHeight, MaxHeight));
                var maxSize = new Size(Math.Max(MaxWidth, MinWidth), Math.Max(MinHeight, MaxHeight));
                size = this.PerformLayout(minSize, maxSize);
                ResizeRequested = false;
            }
            SetMeasuredDimension(ResolveSize((int)size.Width, widthMeasureSpec), ResolveSize((int)size.Height, heightMeasureSpec));
            DroidFactory.ShowKeyboard(TextBase.CurrentFocus);
        }

        /// <summary>
        /// Called from layout when this view should assign a size and position to each of its children.
        /// Derived classes with children should override this method and call layout on each of their children.
        /// </summary>
        /// <param name="changed"><c>true</c> if this is a new size or position for this view; otherwise <c>false</c></param>
        /// <param name="l">Left position, relative to parent</param>
        /// <param name="t">Top position, relative to parent</param>
        /// <param name="r">Right position, relative to parent</param>
        /// <param name="b">Bottom position, relative to parent</param>
        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            if (changed)
                SetMeasuredDimension(r - l, b - t);
        }

        #endregion

        #region IGrid members

        public ColumnCollection Columns { get; private set; }

        public RowCollection Rows { get; private set; }

        public Thickness Padding
        {
            get { return _padding; }
            set
            {
                if (_padding == value) return;
                _padding = value;
                OnPropertyChanged();
                ResizeRequested = true;
            }
        }
        private Thickness _padding;

        public IEnumerable<IElement> Children
        {
            get
            {
                for (var i = 0; i < ChildCount; i++)
                {
                    var element = GetChildAt(i) as IElement;
                    if (element != null) yield return element.Pair as IElement ?? element;
                }
            }
        }

        public void AddChild(IElement element)
        {
            if (element == null) { throw new ArgumentNullException(nameof(element)); }
            var concreteControl = DroidFactory.GetNativeObject<View>(element, nameof(element));
            if (concreteControl == null)
            {
                throw new ArgumentException("Element must provide a native Pair.", nameof(element));
            }
            if (concreteControl.Parent == this) return;
            var oldElement = string.IsNullOrEmpty(element.ID) ? null : Children.FirstOrDefault(c => c.ID == element.ID);
            if (oldElement != null)
            {
                RemoveChild(oldElement);
            }
            (concreteControl.Parent as ViewGroup)?.RemoveView(concreteControl);
            AddView(concreteControl);
        }

        public void RemoveChild(IElement control)
        {
            var concreteControl = DroidFactory.GetNativeObject<View>(control, nameof(control));
            RemoveView(concreteControl);
        }

        internal void SetSubmission(string id, string value)
        {
            if (id == null || Parent == null) return;
            var values = GetSubmissions();
            if (values != null) values[id] = value;
        }

        private IDictionary<string, string> GetSubmissions()
        {
            return (Parent as IListView)?.GetSubmissionValues() ??
                   (Parent as IGridView)?.GetSubmissionValues();
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (Handle == IntPtr.Zero) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool ResizeRequested { get; set; }

        public IPairable Pair
        {
            get { return _pair; }
            set
            {
                if (_pair != null || value == null) return;
                _pair = value;
                _pair.Pair = this;
            }
        }
        internal IPairable _pair;
    }
}