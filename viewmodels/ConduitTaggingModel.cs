namespace JPMorrow.UI.ViewModels
{
	#region using
	using JPMorrow.Tools.Collections;
	using JPMorrow.Custom.Revit.Documents;
	using System;
	using System.IO;
	using System.Collections.Generic;
	using System.Linq;
	using Autodesk.Revit.DB;
	using Autodesk.Revit.DB.Electrical;
	using System.Collections.ObjectModel;
	using System.Runtime.CompilerServices;
	using System.ComponentModel;
	using System.Windows.Input;
	using System.Windows.Threading;
	using System.Windows;
	using MainApp;
    using JPMorrow.Tools.Revit;
	using JPMorrow.Revit.Documents;
	using JPMorrow.Revit.Tools;
	using JPMorrow.Tools.Diagnostics;
    using JPMorrow.Tools.Data;
	using System.Windows.Controls;

    using Visibility = System.Windows.Visibility;
	using JPMorrow.ConduitTagging;

	#endregion

	public partial class ConduitTaggingModel : Presenter
    {
        public ObservableCollection<ConduitPresenter> Conduit_Items { get; set; } = new ObservableCollection<ConduitPresenter>();
        public ObservableCollection<string> Tag_Size_Items { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<String> Tag_View_Items { get; set; } = new ObservableCollection<String>();
        public ObservableCollection<String> Tag_Orient_Items { get; set; } = new ObservableCollection<String>();

        //model info
        public ModelInfo Info { get; private set; }

        //Commands
        public ICommand DirtyCloseCmd => new RelayCommand<Window>(DirtyClose);
        public ICommand CloseCmd => new RelayCommand<Window>(Close);
        public ICommand TagViewRefreshCmd => new RelayCommand<Window>(TagViewRefresh);
        public ICommand AddCmd => new RelayCommand<Window>(AddConduit);
        public ICommand RemoveCmd => new RelayCommand<Window>(RemoveConduit);
        public ICommand SelectionChangedCmd => new RelayCommand<DataGrid>(RunSelectionChanged);
        public ICommand TagCmd => new RelayCommand<Window>(PlaceTags);

        public int Tag_Size_Sel { get; set; }
        public int Tag_View_Sel { get; set; }
        public int Tag_Orient_Sel { get; set; }

        /// <summary>
        /// The ConduitTaggingModel
        /// </summary>
        public ConduitTaggingModel(ModelInfo info)
        {
            Info = info;

            RefreshViews();
            RefreshTagSizes();
            Tag_Orient_Items = new ObservableCollection<string>(new string[]{ "horizontal", "vertical" });
            RaisePropertyChanged("Tag_Orient_Items");
        }

        //get views
        public void RefreshViews()
        {
            Tag_View_Items.Clear();
            FilteredElementCollector view_coll = new FilteredElementCollector(Info.DOC);
            List<string> view_names = view_coll.OfCategory(BuiltInCategory.OST_Views).Where(x => (x as View).ViewType == ViewType.FloorPlan).Select(y => y.Name).ToList();
            view_names.ForEach(x => Tag_View_Items.Add(x));
            RaisePropertyChanged("Tag_View_Items");
        }

        public void RefreshConduitGrid()
        {

        }

        public void RefreshTagSizes()
        {
            string[] sizes = LabelFactory.Label_Sizes;
            Tag_Size_Items = new ObservableCollection<string>(sizes);
            RaisePropertyChanged("Tag_Size_Items");
        }
    }

    #region Data Binding Presenters
    /// <summary>
    /// Single Run Entry ListBox Binding
    /// </summary>
    public class ConduitPresenter : Presenter
    {
        public Element Value;

        private string from;
        public string From { get { return from; } set {
            from = value;
            RaisePropertyChanged("Conduit_Items");
        } }

        private string to;
        public string To { get { return to; } set {
            to = value;
            RaisePropertyChanged("Conduit_Items");
        } }

        private string type;
        public string Type { get { return type; } set {
            type = value;
            RaisePropertyChanged("Conduit_Items");
        } }

        public ConduitPresenter(Element value)
        {
            Value = value;
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            string p(Element x, string str) => String.IsNullOrWhiteSpace(x.LookupParameter(str).AsString()) ? "UNSET" : x.LookupParameter(str).AsString();
            From = p(Value, "From");
            To = p(Value, "To");
            Type = Value.Name;
            RaisePropertyChanged("Conduit_Items");
        }

        //Item Selection Bindings
        public bool isSel;
        public bool IsSelected { get { return isSel; } set {
            isSel = value;
            RaisePropertyChanged("Conduit_Items");
        } }
    }

    /// <summary>
    /// Default Presenter: Just Presents a string value as a listbox item,
    /// can replace with an object for more complex listbox databindings
    /// </summary>
    public class ItemPresenter : Presenter
    {
        private readonly string _value;
        public ItemPresenter(string value) => _value = value;
    }
    #endregion

    #region Inherited Classes
    public abstract class Presenter : INotifyPropertyChanged
    {
         public event PropertyChangedEventHandler PropertyChanged;

         protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
         {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
         }
    }
    #endregion

    public class RelayCommand<T> : ICommand
    {
        #region Fields

        readonly Action<T> _execute = null;
        readonly Predicate<T> _canExecute = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="DelegateCommand{T}"/>.
        /// </summary>
        /// <param name="execute">Delegate to execute when Execute is called on the command.  This can be null to just hook up a CanExecute delegate.</param>
        /// <remarks><seealso cref="CanExecute"/> will always return true.</remarks>
        public RelayCommand(Action<T> execute)
            : this(execute, null)
        { }

        /// <summary>
        /// Creates a new command.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion

        #region ICommand Members

        ///<summary>
        ///Defines the method that determines whether the command can execute in its current state.
        ///</summary>
        ///<param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        ///<returns>
        ///true if this command can be executed; otherwise, false.
        ///</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute((T)parameter);
        }

        ///<summary>
        ///Occurs when changes occur that affect whether or not the command should execute.
        ///</summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        ///<summary>
        ///Defines the method to be called when the command is invoked.
        ///</summary>
        ///<param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to <see langword="null" />.</param>
        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        #endregion
    }

}