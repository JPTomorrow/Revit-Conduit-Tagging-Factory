using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using JPMorrow.Revit.Documents;
using JPMorrow.Revit.Tools;
using JPMorrow.Tools.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JPMorrow.ConduitTagging
{
	public struct TOrientation
	{
		private readonly string orient_str;
		private readonly TagOrientation orient_real;

		public string Orientation_Str { get => orient_str; }
		public TagOrientation Orientation_Real { get => orient_real; }

		public TOrientation(string os, TagOrientation or)
		{
			orient_str = os;
			orient_real = or;
		}
	}
	public struct LabelFactory
	{
		private static string[] labelsizes { get; } = new[] { "1/16\"", "3/32\"" };
		public static string[] Label_Sizes { get {
				string[] sizesCopy = new string[labelsizes.Length];
				labelsizes.CopyTo(sizesCopy, 0);
				return sizesCopy;
		} }

		public static TOrientation[] Orientations { get; } = new TOrientation[] {
			new TOrientation("horizontal", TagOrientation.Horizontal),
			new TOrientation("vertical", TagOrientation.Vertical),
		};

		public static Dictionary<string, string> Size_Family_Name_Swap { get; } = new Dictionary<string, string>() {
			{ labelsizes[0], "lf1.rfa" },
			{ labelsizes[1], "lf2.rfa" },
		};

		private static IndependentTag CreateTag (ModelInfo info, View view, Element tag_el, string tag_name, TagOrientation orientation, string fam_path)
		{
			Family tag_fam = null;
			FilteredElementCollector elColl = new FilteredElementCollector(info.DOC);

			//find tag in document
			try
			{
				tag_fam = elColl
					.OfClass(typeof(Family))
					.Cast<Family>()
					.Where(x => x.Name == tag_name.Split('.').First()).First();
			}
			catch
			{
				tag_fam = null;
			}

			if(tag_fam == null)
				info.DOC.LoadFamily(fam_path + tag_name, out tag_fam);

			if (tag_fam == null) return null;
			Curve locCurve = (tag_el.Location as LocationCurve).Curve;

			Reference reference = new Reference(tag_el);
			IndependentTag tag = IndependentTag.Create(info.DOC, view.Id, reference, true, TagMode.TM_ADDBY_CATEGORY, orientation, locCurve.Evaluate(0.5, true));

			tag.ChangeTypeId(tag_fam.GetFamilySymbolIds().First());
			return tag;
		}

		/// <summary>
		/// Activate the familySymbol in the Revit Model
		/// </summary>
		/// <param name="sym">the FamilySymbol to activate</param>
		private void ActivateSymbol(FamilySymbol sym)
		{
			if (sym == null)
			{
				return;
			}
			if (!sym.IsActive)
			{
				sym.Activate();
			}
		}

		/// <summary>
		/// Tag Creation
		/// </summary>
		private static PlaceTags handler_place_tags = null;
		private static ExternalEvent exEvent_place_tags = null;

		public static void TagElementsSignUp()
		{
			handler_place_tags = new PlaceTags();
			exEvent_place_tags = ExternalEvent.Create(handler_place_tags);
		}

		public static void TagElements(ModelInfo info, View view, ElementId[] ids, string fam_path, string tag_orient, string size)
		{
			handler_place_tags.Info = info;
			handler_place_tags.View = view;
			handler_place_tags.Conduit_Ids = ids;
			handler_place_tags.Family_Path = fam_path;
			handler_place_tags.Orientation = Orientations.ToList().Where(x => x.Orientation_Str == tag_orient).First().Orientation_Real;
			handler_place_tags.Tag_Size = size;
			exEvent_place_tags.Raise();
		}

		//EXTERNAL EVENT
		public class PlaceTags : IExternalEventHandler
		{
			public ElementId[] Conduit_Ids { get; set; }
			public ModelInfo Info { get; set; }
			public View View { get; set; }
			public TagOrientation Orientation { get; set; }
			public string Family_Path { get; set; }
			public string Tag_Size { get; set; }

			/// <summary>
			/// place tags
			/// </summary>
			public void Execute(UIApplication app)
			{
				using (TransactionGroup tgx = new TransactionGroup(Info.DOC, "Placing Tags"))
				{
					tgx.Start();

					foreach (ElementId id in Conduit_Ids)
					{
						Element conduit = Info.DOC.GetElement(id);

						using (Transaction tx = new Transaction(Info.DOC, "fixing length"))
						{
							tx.Start();
							//factory and search system
							RunNetwork rn = ConduitNetwork.GetRunNetworkToJbox(Info.DOC.GetElement(id));
							ConduitRunInfo cri = RCRP_p.ParseNetwork(rn, Info);
							IndependentTag tag = LabelFactory.CreateTag(Info, View, conduit, LabelFactory.Size_Family_Name_Swap[Tag_Size],  Orientation, Family_Path);

							//get length of run
							double len_of_run = 0.0;
							foreach (int con_id in cri.child_conduit_ids)
							{
								//get total length of run
								if (conduit.LookupParameter("Length") != null &&
									conduit.LookupParameter("Length").HasValue)
								{
									len_of_run += conduit.LookupParameter("Length").AsDouble();
								}
								else if (conduit.LookupParameter("Conduit Length") != null)
								{
									string potential_angle = Regex.Match(conduit.LookupParameter("Angle").AsValueString(), @"\d\d").Value;
									if(String.IsNullOrWhiteSpace(potential_angle))
										potential_angle = Regex.Match(conduit.LookupParameter("Angle").AsValueString(), @"\d").Value;

									double centralAngle = double.Parse(potential_angle);
									double bendRad = conduit.LookupParameter("Bend Radius").AsDouble();
									double fittingLength = ((2 * Math.PI * bendRad) * (centralAngle / 360)) + (conduit.LookupParameter("Conduit Length").AsDouble() * 2);
									len_of_run += fittingLength;
								}
							}
							conduit.LookupParameter("CTF_Total_Length").Set(len_of_run);
							tx.Commit();
						}
					}
					tgx.Assimilate();
				}

			}

			public string GetName()
			{
				return "Place Tags";
			}
		}
	}
}
