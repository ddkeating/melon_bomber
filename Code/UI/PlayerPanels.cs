using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Sandbox;
using Sandbox.UI;

public class PlayerPanels : Panel
{
    // To be implemented in the future.
    public Label MyLabel { get; set; }

    public PlayerPanels()
    {
        MyLabel = new Label();
        MyLabel.Parent = this;
    }
}
