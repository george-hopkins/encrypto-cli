namespace Encrypto.Core.Patcher
{
	using System;
	using System.IO;
	using System.Linq;
	using Mono.Cecil;
	using Mono.Cecil.Cil;

	static class Program
	{
		public static int Main (string[] args)
		{
			if (args.Length != 2) {
				Console.Error.WriteLine ("Usage: encrypto-core-patcher <path/to/Encrypto.Core.dll> <patched.dll>");
				return 1;
			}

			Patch (args [0], args [1]);

			return 0;
		}

		/// <summary>
		/// Patches Encrypto.Core assembly file to use correct directory separators when extracting files
		/// </summary>
		private static void Patch (string originalPath, string patchedPath)
		{
			var module = ModuleDefinition.ReadModule (originalPath);
			var type = module.Types.Single (t => t.Name == "EncryptedArchive");
			var extractMethod = type.Methods.Single (m => m.Name == "ExtractAllItems");
			var il = extractMethod.Body.GetILProcessor ();

			var callPathCombine = extractMethod.Body.Instructions.Single (i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "Combine");

			// ldc.i4.s 0x5c
			var loadBackslash = il.Create (OpCodes.Ldc_I4_S, (sbyte)'\\');
			il.InsertAfter (callPathCombine, loadBackslash);

			// ldsfld char [mscorlib]System.IO.Path::DirectorySeparatorChar
			var loadDirectorySeparator = il.Create (OpCodes.Ldsfld, module.Import (typeof(Path).GetField ("DirectorySeparatorChar")));
			il.InsertAfter (loadBackslash, loadDirectorySeparator);

			// callvirt instance string string::Replace(char, char)
			var callStringReplace = il.Create (OpCodes.Callvirt, module.Import (typeof(string).GetMethod ("Replace", new Type[] {
				typeof(char),
				typeof(char)
			})));
			il.InsertAfter (loadDirectorySeparator, callStringReplace);

			module.Write (patchedPath);
		}
	}
}
