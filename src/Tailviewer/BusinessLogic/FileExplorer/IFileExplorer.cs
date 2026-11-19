namespace Tailviewer.BusinessLogic.FileExplorer
{
	/// <summary>Opens folders and selects existing filesystem items in the system file explorer.</summary>
	public interface IFileExplorer
	{
		/// <summary>Opens a folder and selects the specified immediate children.</summary>
		void SelectFiles(string folder, params string[] filesToSelect);

		/// <summary>Opens the containing folder and selects one item.</summary>
		void SelectFile(string filePath);

		/// <summary>Opens the specified folder.</summary>
		void OpenFolder(string folderPath);
	}
}
