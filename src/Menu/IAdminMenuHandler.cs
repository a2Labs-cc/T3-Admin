using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace Furien_Admin.Menu;

public interface IAdminMenuHandler
{
    IMenuAPI CreateMenu(IPlayer player);
}
