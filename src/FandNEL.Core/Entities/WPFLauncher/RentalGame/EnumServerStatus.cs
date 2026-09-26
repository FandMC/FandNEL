namespace FandNEL.Core.Entities.WPFLauncher.RentalGame;

public enum EnumServerStatus
{
	None = -1,
	ServerOff,
	ServerOn,
	Uninitialized,
	Opening,
	Closing,
	OutOfDate,
	SaveCleaning,
	Resetting,
	Upgrading,
	DiscOverflow
}
