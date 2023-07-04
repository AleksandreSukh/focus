#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

//TODO: move to utils
public class PathHelpers
{
	public static readonly HashSet<char> InvalidFileNameChars = Path.GetInvalidFileNameChars().ToHashSet();
}
