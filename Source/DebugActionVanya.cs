using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using RimWorld.IO;
using Verse.Steam;

namespace VanyaTransTool
{
    public class DebugActionVanya
    {
        [DebugAction("Translation", null, false, false, allowedGameStates = AllowedGameStates.Entry)]
        private static void CleanUpTransFilesForMod()
        {
			List<DebugMenuOption> list = new List<DebugMenuOption>();
			foreach (ModMetaData modMetaData in from x in ModsConfig.ActiveModsInLoadOrder
												where !x.Official
												select x)
            {
				list.Add(new DebugMenuOption(modMetaData.Name, DebugMenuOptionMode.Action, delegate ()
                {
					ModTransFilesCleaner.CleanupTranslationFiles(modMetaData);

				}));
			}
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
		}


		[DebugAction("Translation", null, false, false, allowedGameStates = AllowedGameStates.Entry)]
		private static void CleanUpTransFilesAllMods()
		{
			foreach (ModMetaData modMetaData in from x in ModsConfig.ActiveModsInLoadOrder
												where !x.Official
												select x)
			{
				ModTransFilesCleaner.CleanupTranslationFiles(modMetaData);
			}
		}


	}

    public static class ModTransFilesCleaner
    {
		public static void CleanupTranslationFiles(ModMetaData modMetaData)
        {
			LoadedLanguage curLang = LanguageDatabase.activeLanguage;
			LoadedLanguage english = LanguageDatabase.defaultLanguage;
			if (curLang == english)
			{
				return;
			}
			if (curLang.anyKeyedReplacementsXmlParseError || curLang.anyDefInjectionsXmlParseError)
			{
				string value = curLang.lastKeyedReplacementsXmlParseErrorInFile ?? curLang.lastDefInjectionsXmlParseErrorInFile;
				Messages.Message("MessageCantCleanupTranslationFilesBeucaseOfXmlError".Translate(value), MessageTypeDefOf.RejectInput, false);
				return;
			}
			english.LoadData();
			curLang.LoadData();
			ModTransFilesCleaner.DoCleanupTranslationFiles(modMetaData);
		}



		private static void DoCleanupTranslationFiles(ModMetaData modMetaData)
		{
			if (LanguageDatabase.activeLanguage == LanguageDatabase.defaultLanguage)
			{
				return;
			}
			try
			{
				try
				{
					ModTransFilesCleaner.CleanupKeyedTranslations(modMetaData);
				}
				catch (Exception arg)
				{
					Log.Error("Could not cleanup keyed translations: " + arg);
				}
				try
				{
					ModTransFilesCleaner.CleanupDefInjections(modMetaData);
				}
				catch (Exception arg2)
				{
					Log.Error("Could not cleanup def-injections: " + arg2);
				}
				string value = ModTransFilesCleaner.GetLanguageFolderPath(LanguageDatabase.activeLanguage, modMetaData.RootDir.FullName);
				Messages.Message("MessageTranslationFilesCleanupDone".Translate(value), MessageTypeDefOf.TaskCompletion, false);
			}
			catch (Exception arg4)
			{
				Log.Error("Could not cleanup translation files: " + arg4);
			}
		}



		private static void CleanupKeyedTranslations(ModMetaData modMetaData)
		{
			LoadedLanguage activeLanguage = LanguageDatabase.activeLanguage;
			LoadedLanguage english = LanguageDatabase.defaultLanguage;
			List<LoadedLanguage.KeyedReplacement> list = (from x in activeLanguage.keyedReplacements
														  where !x.Value.isPlaceholder && !english.HaveTextForKey(x.Key, false)
														  select x.Value).ToList<LoadedLanguage.KeyedReplacement>();
			//HashSet<LoadedLanguage.KeyedReplacement> writtenUnusedKeyedTranslations = new HashSet<LoadedLanguage.KeyedReplacement>();

			string languageFolderPath = ModTransFilesCleaner.GetLanguageFolderPath(activeLanguage, modMetaData.RootDir.FullName);
			string text = Path.Combine(languageFolderPath, "CodeLinked");
			string text2 = Path.Combine(languageFolderPath, "Keyed");
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			if (directoryInfo.Exists)
			{
				if (!Directory.Exists(text2))
				{
					Directory.Move(text, text2);
					Thread.Sleep(1000);
					directoryInfo = new DirectoryInfo(text2);
				}
			}
			else
			{
				directoryInfo = new DirectoryInfo(text2);
			}
			DirectoryInfo directoryInfo2 = new DirectoryInfo(Path.Combine(ModTransFilesCleaner.GetLanguageFolderPath(english, modMetaData.RootDir.FullName), "Keyed"));
			if (!directoryInfo2.Exists)
			{
				if (modMetaData.IsCoreMod)
				{
					Log.Error("English keyed translations folder doesn't exist.");
				}
				if (!directoryInfo.Exists)
				{
					return;
				}
			}
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
			foreach (FileInfo fileInfo in directoryInfo.GetFiles("*.xml", SearchOption.AllDirectories))
			{
				try
				{
					fileInfo.Delete();
				}
				catch (Exception ex)
				{
                    Log.Error("Could not delete " + fileInfo.Name + ": " + ex);
                }
			}
			foreach (FileInfo fileInfo2 in directoryInfo2.GetFiles("*.xml", SearchOption.AllDirectories))
			{
				try
				{
					string path = new Uri(directoryInfo2.FullName + Path.DirectorySeparatorChar.ToString()).MakeRelativeUri(new Uri(fileInfo2.FullName)).ToString();
					string text3 = Path.Combine(directoryInfo.FullName, path);
					Directory.CreateDirectory(Path.GetDirectoryName(text3));
					fileInfo2.CopyTo(text3);
				}
				catch (Exception ex2)
				{
                    Log.Error("Could not copy " + fileInfo2.Name + ": " + ex2);
                }
			}
			foreach (FileInfo fileInfo3 in directoryInfo.GetFiles("*.xml", SearchOption.AllDirectories))
			{
				try
				{
					XDocument xdocument = XDocument.Load(fileInfo3.FullName, LoadOptions.PreserveWhitespace);
					XElement xelement = xdocument.DescendantNodes().OfType<XElement>().FirstOrDefault<XElement>();
					if (xelement != null)
					{
						try
						{
							foreach (XNode xnode in xelement.DescendantNodes().ToArray<XNode>())
							{
								XElement xelement2 = xnode as XElement;
								if (xelement2 != null)
								{
									foreach (XNode xnode2 in xelement2.DescendantNodes().ToArray<XNode>())
									{
										try
										{
											XText xtext = xnode2 as XText;
											if (xtext != null && !xtext.Value.NullOrEmpty())
											{
												string comment = " EN: " + xtext.Value + " ";
												xnode.AddBeforeSelf(new XComment(ModTransFilesCleaner.SanitizeXComment(comment)));
												xnode.AddBeforeSelf(Environment.NewLine);
												xnode.AddBeforeSelf("  ");
											}
										}
										catch (Exception ex3)
										{
                                            Log.Error("Could not add comment node in " + fileInfo3.Name + ": " + ex3);
                                        }
										xnode2.Remove();
									}
									try
									{
										TaggedString taggedString;
										if (activeLanguage.TryGetTextFromKey(xelement2.Name.ToString(), out taggedString))
										{
											if (!taggedString.NullOrEmpty())
											{
												xelement2.Add(new XText(taggedString.Replace("\n", "\\n").RawText));
											}
										}
										else
										{
											xelement2.Add(new XText("TODO"));
										}
									}
									catch (Exception ex4)
									{
                                        Log.Error("Could not add existing translation or placeholder in " + fileInfo3.Name + ": " + ex4);
                                    }
								}
							}
							bool flag = false;
							foreach (LoadedLanguage.KeyedReplacement keyedReplacement in list)
							{
								if (new Uri(fileInfo3.FullName).Equals(new Uri(keyedReplacement.fileSourceFullPath)))
								{
									if (!flag)
									{
										xelement.Add("  ");
										xelement.Add(new XComment(" UNUSED "));
										xelement.Add(Environment.NewLine);
										flag = true;
									}
									XElement xelement3 = new XElement(keyedReplacement.key);
									if (keyedReplacement.isPlaceholder)
									{
										xelement3.Add(new XText("TODO"));
									}
									else if (!keyedReplacement.value.NullOrEmpty())
									{
										xelement3.Add(new XText(keyedReplacement.value.Replace("\n", "\\n")));
									}
									xelement.Add("  ");
									xelement.Add(xelement3);
									xelement.Add(Environment.NewLine);
									//writtenUnusedKeyedTranslations.Add(keyedReplacement);
								}
							}
							if (flag)
							{
								xelement.Add(Environment.NewLine);
							}
						}
						finally
						{
							ModTransFilesCleaner.SaveXMLDocumentWithProcessedNewlineTags(xdocument.Root, fileInfo3.FullName);
						}
					}
				}
				catch (Exception ex5)
				{
                    Log.Error("Could not process " + fileInfo3.Name + ": " + ex5);
                }
			}
		}

		private static void CleanupDefInjections(ModMetaData modMetaData)
		{
			//foreach (ModMetaData modMetaData in ModsConfig.ActiveModsInLoadOrder)
			string languageFolderPath = ModTransFilesCleaner.GetLanguageFolderPath(LanguageDatabase.activeLanguage, modMetaData.RootDir.FullName);
			string text = Path.Combine(languageFolderPath, "DefLinked");
			string text2 = Path.Combine(languageFolderPath, "DefInjected");
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			if (directoryInfo.Exists)
			{
				if (!Directory.Exists(text2))
				{
					Directory.Move(text, text2);
					Thread.Sleep(1000);
					directoryInfo = new DirectoryInfo(text2);
				}
			}
			else
			{
				directoryInfo = new DirectoryInfo(text2);
			}
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
			foreach (FileInfo fileInfo in directoryInfo.GetFiles("*.xml", SearchOption.AllDirectories))
			{
				try
				{
					fileInfo.Delete();
				}
				catch (Exception ex)
				{
                    Log.Error("Could not delete " + fileInfo.Name + ": " + ex);
                }
			}
			foreach (Type type in GenDefDatabase.AllDefTypesWithDatabases())
			{
				try
				{
					ModTransFilesCleaner.CleanupDefInjectionsForDefType(type, directoryInfo.FullName, modMetaData);
				}
				catch (Exception ex2)
				{
                    Log.Error("Could not process def-injections for type " + type.Name + ": " + ex2);
                }
			}
		}

        private static void CleanupDefInjectionsForDefType(Type defType, string defInjectionsFolderPath, ModMetaData mod)
        {
            LoadedLanguage activeLanguage = LanguageDatabase.activeLanguage;
            List<KeyValuePair<string, DefInjectionPackage.DefInjection>> list = (from x in activeLanguage.defInjections.Where((DefInjectionPackage x) => x.defType == defType).SelectMany((DefInjectionPackage x) => x.injections)
                                                                                 where !x.Value.isPlaceholder && x.Value.ModifiesDefFromModOrNullCore(mod, defType)
                                                                                 select x).ToList();
            Dictionary<string, DefInjectionPackage.DefInjection> dictionary = new Dictionary<string, DefInjectionPackage.DefInjection>();
            foreach (KeyValuePair<string, DefInjectionPackage.DefInjection> item2 in list)
            {
                if (!dictionary.ContainsKey(item2.Value.normalizedPath))
                {
                    dictionary.Add(item2.Value.normalizedPath, item2.Value);
                }
            }
            if (defType == typeof(BackstoryDef))
            {
                foreach (DefInjectionPackage.DefInjection legacyBackstoryTranslation in BackstoryTranslationUtility.GetLegacyBackstoryTranslations(activeLanguage.AllDirectories))
                {
                    if (!dictionary.ContainsKey(legacyBackstoryTranslation.path))
                    {
                        dictionary.Add(legacyBackstoryTranslation.path, legacyBackstoryTranslation);
                    }
                }
            }
            List<PossibleDefInjection> possibleDefInjections = new List<PossibleDefInjection>();
            DefInjectionUtility.ForEachPossibleDefInjection(defType, delegate (string suggestedPath, string normalizedPath, bool isCollection, string str, IEnumerable<string> collection, bool translationAllowed, bool fullListTranslationAllowed, FieldInfo fieldInfo, Def def)
            {
                if (translationAllowed)
                {
                    PossibleDefInjection item = new PossibleDefInjection
                    {
                        suggestedPath = suggestedPath,
                        normalizedPath = normalizedPath,
                        isCollection = isCollection,
                        fullListTranslationAllowed = fullListTranslationAllowed,
                        curValue = str,
                        curValueCollection = collection,
                        fieldInfo = fieldInfo,
                        def = def
                    };
                    possibleDefInjections.Add(item);
                }
            }, mod);
            if (!possibleDefInjections.Any() && !list.Any())
            {
                return;
            }
            List<KeyValuePair<string, DefInjectionPackage.DefInjection>> source = list.Where((KeyValuePair<string, DefInjectionPackage.DefInjection> x) => !x.Value.injected).ToList();
            foreach (string fileName in possibleDefInjections.Select((PossibleDefInjection x) => GetSourceFile(x.def)).Concat(source.Select((KeyValuePair<string, DefInjectionPackage.DefInjection> x) => x.Value.fileSource)).Distinct())
            {
                try
                {
                    XDocument xDocument = new XDocument();
                    bool flag = false;
                    try
                    {
                        XElement xElement = new XElement("LanguageData");
                        xDocument.Add(xElement);
                        xElement.Add(new XComment("NEWLINE"));
                        List<PossibleDefInjection> source2 = possibleDefInjections.Where((PossibleDefInjection x) => GetSourceFile(x.def) == fileName).ToList();
                        List<KeyValuePair<string, DefInjectionPackage.DefInjection>> source3 = source.Where((KeyValuePair<string, DefInjectionPackage.DefInjection> x) => x.Value.fileSource == fileName).ToList();
                        foreach (string defName in from x in source2.Select((PossibleDefInjection x) => x.def.defName).Concat(source3.Select((KeyValuePair<string, DefInjectionPackage.DefInjection> x) => x.Value.DefName)).Distinct()
                                                   orderby x
                                                   select x)
                        {
                            try
                            {
                                IEnumerable<PossibleDefInjection> enumerable = source2.Where((PossibleDefInjection x) => x.def.defName == defName);
                                //IEnumerable<KeyValuePair<string, DefInjectionPackage.DefInjection>> enumerable2 = source3.Where((KeyValuePair<string, DefInjectionPackage.DefInjection> x) => x.Value.DefName == defName);
                                if (enumerable.Any())
                                {
                                    bool flag2 = false;
                                    foreach (PossibleDefInjection item3 in enumerable)
                                    {
                                        if (item3.isCollection)
                                        {
                                            IEnumerable<string> englishList = GetEnglishList(item3.normalizedPath, item3.curValueCollection, dictionary);
                                            bool flag3 = false;
                                            if (englishList != null)
                                            {
                                                int num = 0;
                                                foreach (string item4 in englishList)
                                                {
                                                    _ = item4;
                                                    if (dictionary.ContainsKey(item3.normalizedPath + "." + num))
                                                    {
                                                        flag3 = true;
                                                        break;
                                                    }
                                                    num++;
                                                }
                                            }
                                            if (flag3 || !item3.fullListTranslationAllowed)
                                            {
                                                if (englishList == null)
                                                {
                                                    continue;
                                                }
                                                int num2 = -1;
                                                foreach (string item5 in englishList)
                                                {
                                                    num2++;
                                                    string text = item3.normalizedPath + "." + num2;
                                                    string suggestedPath2 = item3.suggestedPath + "." + num2;
                                                    if (TKeySystem.TrySuggestTKeyPath(text, out var tKeyPath))
                                                    {
                                                        suggestedPath2 = tKeyPath;
                                                    }
                                                    if (!dictionary.TryGetValue(text, out var value))
                                                    {
                                                        value = null;
                                                    }
                                                    if (value == null && !DefInjectionUtility.ShouldCheckMissingInjection(item5, item3.fieldInfo, item3.def))
                                                    {
                                                        continue;
                                                    }
                                                    flag2 = true;
                                                    flag = true;
                                                    try
                                                    {
                                                        if (!item5.NullOrEmpty())
                                                        {
                                                            xElement.Add(new XComment(SanitizeXComment(" EN: " + item5.Replace("\n", "\\n") + " ")));
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Log.Error("Could not add comment node in " + fileName + ": " + ex);
                                                    }
                                                    xElement.Add(GetDefInjectableFieldNode(suggestedPath2, value));
                                                }
                                                continue;
                                            }
                                            bool flag4 = false;
                                            if (englishList != null)
                                            {
                                                foreach (string item6 in englishList)
                                                {
                                                    if (DefInjectionUtility.ShouldCheckMissingInjection(item6, item3.fieldInfo, item3.def))
                                                    {
                                                        flag4 = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            if (!dictionary.TryGetValue(item3.normalizedPath, out var value2))
                                            {
                                                value2 = null;
                                            }
                                            if (value2 == null && !flag4)
                                            {
                                                continue;
                                            }
                                            flag2 = true;
                                            flag = true;
                                            try
                                            {
                                                string text2 = ListToLiNodesString(englishList);
                                                if (!text2.NullOrEmpty())
                                                {
                                                    xElement.Add(new XComment(SanitizeXComment(" EN:\n" + text2.Indented() + "\n  ")));
                                                }
                                            }
                                            catch (Exception ex2)
                                            {
                                                Log.Error("Could not add comment node in " + fileName + ": " + ex2);
                                            }
                                            xElement.Add(GetDefInjectableFieldNode(item3.suggestedPath, value2));
                                            continue;
                                        }
                                        if (!dictionary.TryGetValue(item3.normalizedPath, out var value3))
                                        {
                                            value3 = null;
                                        }
                                        string text3 = ((value3 != null && value3.injected) ? value3.replacedString : item3.curValue);
                                        if (value3 == null && !DefInjectionUtility.ShouldCheckMissingInjection(text3, item3.fieldInfo, item3.def))
                                        {
                                            continue;
                                        }
                                        flag2 = true;
                                        flag = true;
                                        try
                                        {
                                            if (!text3.NullOrEmpty())
                                            {
                                                xElement.Add(new XComment(SanitizeXComment(" EN: " + text3.Replace("\n", "\\n") + " ")));
                                            }
                                        }
                                        catch (Exception ex3)
                                        {
                                            Log.Error("Could not add comment node in " + fileName + ": " + ex3);
                                        }
                                        xElement.Add(GetDefInjectableFieldNode(item3.suggestedPath, value3));
                                    }
                                    if (flag2)
                                    {
                                        xElement.Add(new XComment("NEWLINE"));
                                    }
                                }
                                /*if (!enumerable2.Any())
                                {
                                    continue;
                                }
                                flag = true;
                                xElement.Add(new XComment(" UNUSED "));
                                foreach (KeyValuePair<string, DefInjectionPackage.DefInjection> item7 in enumerable2)
                                {
                                    xElement.Add(GetDefInjectableFieldNode(item7.Value.path, item7.Value));
                                }
                                xElement.Add(new XComment("NEWLINE"));*/
                            }
                            catch (Exception ex4)
                            {
                                Log.Error("Could not process def-injections for def " + defName + ": " + ex4);
                            }
                        }
                    }
                    finally
                    {
                        if (flag)
                        {
                            string text4 = Path.Combine(defInjectionsFolderPath, defType.Name);
                            Directory.CreateDirectory(text4);
                            SaveXMLDocumentWithProcessedNewlineTags(xDocument, Path.Combine(text4, fileName));
                        }
                    }
                }
                catch (Exception ex5)
                {
                    Log.Error("Could not process def-injections for file " + fileName + ": " + ex5);
                }
            }
        }


        public static string GetLanguageFolderPath(LoadedLanguage language, string modRootDir)
        {
            return Path.Combine(Path.Combine(modRootDir, "Languages"), language.folderName);
        }

        private static void SaveXMLDocumentWithProcessedNewlineTags(XNode doc, string path)
        {
            File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" + doc.ToString().Replace("<!--NEWLINE-->", "").Replace("&gt;", ">"), Encoding.UTF8);
        }

        private static string ListToLiNodesString(IEnumerable<string> list)
        {
            if (list == null)
            {
                return "";
            }
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string item in list)
            {
                stringBuilder.Append("<li>");
                if (!item.NullOrEmpty())
                {
                    stringBuilder.Append(item.Replace("\n", "\\n"));
                }
                stringBuilder.Append("</li>");
                stringBuilder.AppendLine();
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        private static XElement ListToXElement(IEnumerable<string> list, string name, List<Pair<int, string>> comments)
        {
            XElement xElement = new XElement(name);
            if (list != null)
            {
                int num = 0;
                foreach (string item in list)
                {
                    if (comments != null)
                    {
                        for (int i = 0; i < comments.Count; i++)
                        {
                            if (comments[i].First == num)
                            {
                                xElement.Add(new XComment(comments[i].Second));
                            }
                        }
                    }
                    XElement xElement2 = new XElement("li");
                    if (!item.NullOrEmpty())
                    {
                        xElement2.Add(new XText(item.Replace("\n", "\\n")));
                    }
                    xElement.Add(xElement2);
                    num++;
                }
                if (comments != null)
                {
                    for (int j = 0; j < comments.Count; j++)
                    {
                        if (comments[j].First == num)
                        {
                            xElement.Add(new XComment(comments[j].Second));
                        }
                    }
                }
            }
            return xElement;
        }

        private static string AppendXmlExtensionIfNotAlready(string fileName)
        {
            if (!fileName.ToLower().EndsWith(".xml"))
            {
                return fileName + ".xml";
            }
            return fileName;
        }

        private static string GetSourceFile(Def def)
        {
            if (!def.fileName.NullOrEmpty())
            {
                return AppendXmlExtensionIfNotAlready(def.fileName);
            }
            return "Unknown.xml";
        }

        private static IEnumerable<string> GetEnglishList(string normalizedPath, IEnumerable<string> curValue, Dictionary<string, DefInjectionPackage.DefInjection> injectionsByNormalizedPath)
        {
            if (injectionsByNormalizedPath.TryGetValue(normalizedPath, out var value) && value.injected)
            {
                return value.replacedList;
            }
            if (curValue == null)
            {
                return null;
            }
            List<string> list = curValue.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                string key = normalizedPath + "." + i;
                if (injectionsByNormalizedPath.TryGetValue(key, out var value2) && value2.injected)
                {
                    list[i] = value2.replacedString;
                }
            }
            return list;
        }

        private static XElement GetDefInjectableFieldNode(string suggestedPath, DefInjectionPackage.DefInjection existingInjection)
        {
            if (existingInjection == null || existingInjection.isPlaceholder)
            {
                return new XElement(suggestedPath, new XText("TODO"));
            }
            if (existingInjection.IsFullListInjection)
            {
                return ListToXElement(existingInjection.fullListInjection, suggestedPath, existingInjection.fullListInjectionComments);
            }
            XElement xElement;
            if (!existingInjection.injection.NullOrEmpty())
            {
                if (existingInjection.suggestedPath.EndsWith(".slateRef") && ConvertHelper.IsXml(existingInjection.injection))
                {
                    try
                    {
                        return XElement.Parse("<" + suggestedPath + ">" + existingInjection.injection + "</" + suggestedPath + ">");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("Could not parse XML: " + existingInjection.injection + ". Exception: " + ex);
                        xElement = new XElement(suggestedPath);
                        xElement.Add(existingInjection.injection);
                        return xElement;
                    }
                }
                xElement = new XElement(suggestedPath);
                xElement.Add(new XText(existingInjection.injection.Replace("\n", "\\n")));
            }
            else
            {
                xElement = new XElement(suggestedPath);
            }
            return xElement;
        }

        private static string SanitizeXComment(string comment)
        {
            while (comment.Contains("-----"))
            {
                comment = comment.Replace("-----", "- - -");
            }
            while (comment.Contains("--"))
            {
                comment = comment.Replace("--", "- -");
            }
            return comment;
        }

        private class PossibleDefInjection
        {
            public string suggestedPath;

            public string normalizedPath;

            public bool isCollection;

            public bool fullListTranslationAllowed;

            public string curValue;

            public IEnumerable<string> curValueCollection;

            public FieldInfo fieldInfo;

            public Def def;
        }

    }


}
