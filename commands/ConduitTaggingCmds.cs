namespace JPMorrow.UI.ViewModels
{
	using System.Windows;
	using System;
	using System.Linq;
	using System.Collections.Generic;
	using Autodesk.Revit.DB;
	using JPMorrow.Tools.Diagnostics;
	using JPMorrow.Revit.Tools;
	using System.Windows.Controls;
	using JPMorrow.Tools.Data;
    using MainApp;
	using System.Text;
	using System.Text.RegularExpressions;
	using JPMorrow.ConduitTagging;
	using JPMorrow.Revit.Documents;

	public partial class ConduitTaggingModel
    {
        /// <summary>
        /// close windows
        /// </summary>
        /// <param name="window">window to close</param>
        public void DirtyClose(Window window) => window.Close();
        public void Close(Window window)
        {
            try
            {
                window.Close();
            }
            catch(Exception ex)
            {
                debugger.show(err:ex.ToString());
            }
        }

        /// <summary>
        /// Save the load package
        /// </summary>
        public void RunSelectionChanged(DataGrid grid)
        {
            ElementId[] con_to_sel = Conduit_Items.Where(x => x.IsSelected).Select(x => x.Value.Id).ToArray();
            if(!con_to_sel.Any()) return;
            Info.UIDOC.Selection.SetElementIds(con_to_sel);
        }

        /// <summary>
        /// Refresh views
        /// </summary>
        public void TagViewRefresh(Window window)
        {
            RefreshViews();
            debugger.show(header:"Refresh Views", err:"Refreshed available floorplans.");
        }

        /// <summary>
        /// Add conduit
        /// </summary>
        public void AddConduit(Window window)
        {
            List<ElementId> ids_to_add = Info.UIDOC.Selection.GetElementIds().ToList();
            if(!ids_to_add.Any()) return;

            foreach(var id in ids_to_add)
            {
                Element el = Info.DOC.GetElement(id);
                if(el.Category.Name == "Conduits" && !Conduit_Items.Any(x => x.Value.Id == id))
                    Conduit_Items.Add(new ConduitPresenter(el));
            }
            RaisePropertyChanged("Conduit_Items");
        }

        /// <summary>
        /// Remove conduit
        /// </summary>
        public void RemoveConduit(Window window)
        {
            ConduitPresenter[] con_to_rem = Conduit_Items.Where(x => x.IsSelected).ToArray();
            if(!con_to_rem.Any()) return;

            foreach(var presenter in con_to_rem)
            {
                Conduit_Items.Remove(presenter);
            }
            RaisePropertyChanged("Conduit_Items");
        }

        /// <summary>
        /// place the tags
        /// </summary>
        public void PlaceTags(Window window)
        {
            ElementId[] ids_to_tag = Conduit_Items
            .Select(y => y.Value.Id).ToArray();
            if(!ids_to_tag.Any()) return;

            //get all views and match selection
            FilteredElementCollector view_coll = new FilteredElementCollector(Info.DOC);
            Element[] views_to_proc = view_coll.OfCategory(BuiltInCategory.OST_Views).Where(x => x.Name == Tag_View_Items[Tag_View_Sel] ).ToArray();

            //selected view doesnt exist
            if(!views_to_proc.Any())
            {
                debugger.show(err:"Selected view no longer exists. Please refresh the views list and select another one.");
                return;
            }

            //get tag family path
            string fam_file_path = RAP.GetDataDirectory("tag_families", ThisApplication.Application_Base_Path, ThisApplication.Data_Directories, true);

            try
            {
                LabelFactory.TagElements(Info, views_to_proc.Cast<View>().First(), ids_to_tag, fam_file_path, Tag_Orient_Items[Tag_Orient_Sel], Tag_Size_Items[Tag_Size_Sel]);
            }
            catch(Exception ex)
            {
                debugger.show(err:ex.ToString());
            }

        }
    }
}