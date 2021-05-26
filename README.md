# Andraste - The "native" C# Modding Framework

> Britain/Celtic goddess of war, symbolizing invincibility

The Andraste Modding Framework aims to be a solid base for those writing an
in-process modding framework for native (x86, 32bit) Windows applications (Games).

It is mostly the result of generalizing code that I would have written
specifically for one game. Releasing it may help others to quickly re-use
functionality as well as maybe contributing and reviewing decisions made here.

## The Host/Payload Model

As already noted, Andraste is in-process, which means that the mods are loaded
directly into the target process.
This allows for an easy handling when hooking known functions, directly
calling functions and manipulating memory.

The code that resides inside the target application, is thus called the `Payload`.
Since managing mods by the payload _after_ the application has launched isn't
really practicable as are limited in-game UIs, Andraste also has a `Host` component.

The `Host` component thus allows you to build native UIs (WinForms, WPF, ...)
and communicates with the `Payload` using `Pipes`. That way an external
application can manage everything, keeping the payload easy and clean.

## Versioning

Versioning is a difficult topic in the .net scope, as there are, to date, four
different versioning schemes:

- C# Language Version
- .NET Framework Version / .NET Core Version
- .NET Standard
- .NET 5.0

To further complicate the issue, the Version being used by the Launcher's EasyHook/EasyLoad.dll,
dictates the CLR Version that is loaded into the Application / Game.

Thus the best advice we can give is: If the official version of Andraste
doesn't have the right ".net version", try to manually compile it, so it fits you.
If that doesn't work, feel free to raise an issue (given you don't try to target
ancient versions, that don't support C# 8 language features)

While .NET 5.0 is going to replace/deprecate all previous versions, it wasn't
well established at the time of writing as well as there wasn't a big need
nuget dependency wise, that would require us to use .NET 5.0

.NET Standard > 2.0 is not supported by .NET Framework, but some/most? nuget
packages carelessly use .NET Standard 2.1 or .NET Core 3.1.

Unfortunately, we cannot support .NET Core at this time, because that would
require changes to the way, the CLR is loaded into the target process, which
is a bigger undertaking (.NET Core wasn't designed for that).
Additionally, quite some functionality is lost when adhering to .net standard,
such as WinForms, which are used by the DirectX Hook and e.g. Message Boxes.

Thus the decision is to use .NET Standard 2.0 as baseline for the Andraste
Codebase, however with additional conditional compilation for a .NET Framework
Version, containing additional features, that may have not been ported to
.NET Standard 2.0.

We will also supply a version of EasyLoad.dll using the most recent .NET FX
(4.8 at the time of writing) along with a modified version of EasyHook32.dll,
which is required to not have the injection fail in some cases.

The future plan is to keep everything .NET Standard 2.0 compliant, so that
once/if EasyHook is .NET Core compliant, there isn't much adoption required.
So long, NuGet packages that target .NET Core or .NET Standard > 2.0 need to be
compiled manually.

**Warning**: As I had to painfully discover, it's important that the full
chain, starting with Andraste, is using the manually supplied EasyHook libraries
because otherwise, when using mixed assemblies, the entry point cannot be found,
because the types obviously differ.

Since our additions and changes only apply to EasyLoad32.dll and EasyHook32.dll,
it _may_ be possible to use EasyHook.dll from nuget and only supply these files
from the Host into the path where your framework resides (i.e. those are loaded
from the folder that the DLL that gets injected resides in).

Since, however, I don't know yet which approach to take, and we're satisfied
for now as long as either way works, keep an eye on what version Andraste is
build against and/or try both.
