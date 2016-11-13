namespace Encrypto.Cli
{
	using System;
	using System.IO;
	using System.Collections.Generic;
	using Encrypto.Core;
	using CommandLine;
	using CommandLine.Text;

	static class Program
	{
		private const string PasswordVariable = "ENCRYPTO_PASSWORD";

		private class Options
		{
			[VerbOption("encrypt", HelpText = "Encrypt a file or a directory")]
			public EncryptOptions EncryptVerb { get; set; }

			[VerbOption("decrypt", HelpText = "Decrypt a file")]
			public DecryptOptions DecryptVerb { get; set; }
		}

		private class EncryptOptions
		{
			[Option("password", HelpText = "Password")]
			public string Password { get; set; }

			[Option("hint", HelpText = "Password hint")]
			public string PasswordHint { get; set; }

			[ValueOption(0)]
			public string OutputFile { get; set; }

			[ValueList(typeof(List<string>))]
			public List<string> Files { get; set; }
		}

		private class DecryptOptions
		{
			[Option("password", HelpText = "Password")]
			public string Password { get; set; }

			[Option("directory", HelpText = "Output Directory")]
			public string Directory { get; set; }

			[ValueOption(0)]
			public string File { get; set; }
		}

		public static int Main (string[] args)
		{
			string command = null;
			object commandOptions = null;

			if (args.Length == 0 || !CommandLine.Parser.Default.ParseArguments (args, new Options (), (verb, verbOptions) => {
				command = verb;
				commandOptions = verbOptions;
			})) {
				Console.Error.WriteLine ("Could not parse arguments.");
				PrintUsage ();
				return 1;
			}

			if (command == "encrypt") {
				EncryptOptions options = (EncryptOptions)commandOptions;
				string password = options.Password ?? Environment.GetEnvironmentVariable (PasswordVariable);
				try {
					encryptFile (options.Files, options.OutputFile, password, options.PasswordHint);
				} catch (OperationException e) {
					Console.Error.WriteLine (e.Message);
					return 1;
				}
				return 0;
			} else if (command == "decrypt") {
				DecryptOptions options = (DecryptOptions)commandOptions;
				string password = options.Password ?? Environment.GetEnvironmentVariable (PasswordVariable);
				string directory = options.Directory ?? Environment.CurrentDirectory;
				try {
					decryptFile (options.File, directory, password);
				} catch (OperationException e) {
					Console.Error.WriteLine (e.Message);
					return 1;
				}
				return 0;
			} else {
				Console.Error.WriteLine ("Unknown command.");
				PrintUsage ();
				return 1;
			}
		}

		private static void PrintUsage ()
		{
			Console.Error.WriteLine ("Usage: encrypto <command> [<args>]");
			Console.Error.WriteLine ("Commands:");
			Console.Error.WriteLine ("  encrypt [--password=<password>] [--hint=<hint>] <output file> <files...>");
			Console.Error.WriteLine ("  decrypt [--password=<password>] [--directory=<directory>] <file>");
		}

		private static void encryptFile (List<string> files, string outputFile, string password, string hint)
		{
			IEncryptedArchive archive = new EncryptedArchive ();
			archive.ArchiveDelegate = new AssertingDelegate ();
			try {
				foreach (string file in files) {
					var path = Path.GetFullPath (file);
					if (File.Exists (path)) {
						archive.AddItem (path, Path.GetDirectoryName (path));
					} else if (Directory.Exists (path)) {
						addDirectory (archive, path, path);
					}
				}
				archive.Password = password;
				archive.PasswordHint = hint;
				archive.Save (outputFile);
			} finally {
				archive.CloseArchive ();
			}
		}

		private static void decryptFile (string file, string outputDirectory, string password)
		{
			if (!Directory.Exists (outputDirectory)) {
				throw new OperationException ("Output directory does not exist.");
			}

			IEncryptedArchive archive = new EncryptedArchive ();
			archive.ArchiveDelegate = new AssertingDelegate ();
			try {
				archive.Password = password;
				archive.OpenArchive (file);
				archive.ExtractAllItems (outputDirectory);
			} finally {
				archive.CloseArchive ();
			}
		}

		private static void addDirectory (IEncryptedArchive archive, string directory, string root)
		{
			foreach (string f in Directory.GetFiles(directory)) {
				archive.AddItem (f, root);
			}
			foreach (string d in Directory.GetDirectories(directory)) {
				archive.AddItem (d, root);
				addDirectory (archive, d, root);
			}
		}

		private class AssertingDelegate : IEncryptedArchiveDelegate
		{
			public void OperationError (IEncryptedArchive archive, Dictionary<object, object> info)
			{
				throw OperationException.WithError ((EncryptedError)info [StaticResources.kEncryptedArchiveError]);
			}

			public void OperationFinished (IEncryptedArchive archive, Dictionary<object, object> info)
			{
			}

			public void OperationHmacMismatch (IEncryptedArchive archive, IEncryptedArchiveItem forItem)
			{
			}

			public void OperationPasswordNeeded (IEncryptedArchive archive)
			{
			}

			public void OperationProgress (IEncryptedArchive archive, Dictionary<object, object> info)
			{
			}

			public void OperationStarted (IEncryptedArchive archive, Dictionary<object, object> info)
			{
			}
		}

		private class OperationException : Exception
		{
			public readonly EncryptedError Error;

			public OperationException (string message) : base(message)
			{
			}

			public OperationException (string message, EncryptedError error) : base(message)
			{
				Error = error;
			}

			public static OperationException WithError (EncryptedError error)
			{
				if (error.ErrorCode == StaticResources.ENEncryptedArchiveInvalidPassword) {
					return new OperationException ("Invalid password.", error);
				} else {
					return new OperationException (String.Format ("Operation failed ({0}).", error.ErrorCode), error);
				}
			}
		}
	}
}
