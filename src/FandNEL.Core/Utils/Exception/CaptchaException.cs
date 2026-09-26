using System;

namespace FandNEL.Core.Utils.Exception;

public class CaptchaException : System.Exception
{
	public CaptchaException(string message)
		: base(message)
	{
	}

	public static CaptchaException Clone(CaptchaException exception, string append)
	{
		return new CaptchaException(exception.Message + "|" + append);
	}
}
