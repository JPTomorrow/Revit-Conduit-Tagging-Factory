
// @Author: Justin Morrow
// @Company: Marathon Electrical
// @Year: 2019

using System;
using Autodesk.Revit.UI;
using System.Reflection;
using Autodesk.Revit.DB;
using System.Linq;
using JPMorrow.Revit.Documents;
using JPMorrow.UI.Views;
using JPMorrow.Revit.Tools;
using System.IO;
using JPMorrow.Tools.Diagnostics;
using System.Collections.Generic;
using Autodesk.Revit.DB.Electrical;
using JPMorrow.ConduitTagging;

namespace MainApp
{
	[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("58F7B2B7-BF8D-4B39-BBF8-13F7D9AAE97E")]
	public partial class ThisApplication : IExternalCommand
	{
		public static bool TestBed_Debug_Switch {get; set;} = false;
		public static string Application_Base_Path {get; private set;} = null;
		public static  string[] Data_Directories {get;} = new string[] { "tag_families" };

		public Result Execute(ExternalCommandData cData, ref string message, ElementSet elements)
        {
			//set revit documents
			ModelInfo revit_info = ModelInfo.StoreDocuments(cData);

			// set app path
			Assembly assem = Assembly.GetExecutingAssembly();
			UriBuilder uri = new UriBuilder(assem.CodeBase);
			string module_path = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
			Application_Base_Path = RAP.GetApplicationBasePath(module_path, assem.GetName().Name, String.Join, TestBed_Debug_Switch);

			//create all data directories if they do not exist
			DirectoryInfo directory_creation(string dir) => Directory.CreateDirectory(dir);
			RAP.GenAppStorageStruct(Application_Base_Path, Data_Directories, directory_creation, Directory.Exists);

			//check shared parameters
			FilteredElementCollector p_coll = new FilteredElementCollector(revit_info.DOC);
			Parameter p = p_coll.OfClass(typeof(Conduit)).First().LookupParameter("CTF_Total_Length");
			bool param_loaded = p == null ? false : true;

			if(!param_loaded)
			{
				debugger.show(err:"Please load a shared parameter called \"CTF_Total_Length\" and run the program again.");
				return Result.Succeeded;
			}

			//sign up external events
			LabelFactory.TagElementsSignUp();

			//load families
			string fam_file_path = RAP.GetDataDirectory("tag_families", Application_Base_Path, Data_Directories, true);

			using(Transaction tx = new Transaction(revit_info.DOC, "Load Families"))
			{
				tx.Start();
				revit_info.DOC.LoadFamily(fam_file_path + "lf1.rfa");
				revit_info.DOC.LoadFamily(fam_file_path + "lf2.rfa");
				tx.Commit();
			}

			ConduitTaggingView ccv = new ConduitTaggingView(revit_info);
			ccv.Show();

			return Result.Succeeded;
        }
	}
}