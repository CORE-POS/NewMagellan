# NewMagellan
This repo is primarily a storage area for pre-built binaries of the NewMagellan driver. For now the code will also live in https://github.com/CORE-POS/IS4C. The directory containing NewMallean's code is https://github.com/CORE-POS/IS4C/tree/master/pos/is4c-nf/scale-drivers/drivers/NewMagellan.

The version hosted in this repo has been restructured to behave more sensibly in Visual Studio with a separate project for each output file.

You can absolutely still build your own driver. Just run "msbuild" or "xbuild" in the project directory. The point of these builds is to provide consistency in deployments.

Current builds are 32-bit, .NET 4.0.

# Dev Notes
Alphabetically, what are these different projects?
* `AxLayer` is provides a standardized interface for ActiveX controls that NewMagellan optionally uses. This exists to allow testing without having the actual ActiveX controls present.
* `Bitmap` deals with signature images. The Signature class can create an image from a list of points. The additional convertor reduces a bitmap to a color depth of 1 (or 1 bit per pixel) which isn't a format natively supported by .NET.
* `Discover` is a library for converting strings to Type objects. This is used to convert configuration file contents into instances of classes.
* `MsgInterface` is an interface for classes that can send and receive messages. The main Magellan class, UDPMsgBox, and SerialPortHandlers all implement this interface and use it to exchange messages with one another.
* `NewMagellan` is the primary project that builds the driver executable.
* `ParallelLayer` is theoretically a cross-platform wrapper for writing to a parallel port. No real-world testing has occurred with it.
* `SPH` is the collection of Serial Port Handlers. All of the low-level logic for interacting with devices lives here.
* `Tests` contains unit tests. This is the only sub-project using F# instead of C#.
* `UDBMsgBox` is a simple UDP server that can receive messages over the network and relay them to another component.
* `USBLayer` is essentially a wrapper around HidSharp with alternatives that exist for backward compatibility. With new development using `USBWrapper_HidSharp is strongly recommended`.

# Basic Operation
NewMagellan starts by reading configuration pairs from ../../../config.json (preferred) or ./ports.conf (legacy). A configuration pair consists of a port, such as `COM1` or `dev/ttyS0` and a serial port handler class, such as `SPH_Magellan_Scale`. For each pair it creates an instance of the specified class, assigns it to the specified port, and starts that object running in its own thread. Then finally it creates an instance of `UDPMsgBox` to handle incoming messages. In this arrangement NewMagellan is the parent, or recipient, for both `UDPMsgBox` and the serial port handlers.

```
USER <---------------- NewMagellan <----> SPH <-----> Device
    \                  /^
     \__> UDPMsgBox __/
```

When the user sends a message to the driver:
1. UDPMsgBox receives the message. It then sends the message along to NewMagellan.
2. NewMagellan relays the message to **all** serial port handlers
3. Each serial port handler can do whatever it wants regarding the message - typically either sending a command to the device or ignoring the message entirely.

When a serial port handler sends a message to the user:
1. The serial port handler passes the message to NewMagellan
2. NewMagellan transmits the message to the user

# Extending
To add support for an additional device, create a new class in the `SPH` project that inherits from `SerialPortHandler` (even if the device is not technically serial this is still where the class should go). The class is only required to implement two methods:
* `void HandleMsg(string msg)` is the method called when NewMagellan needs to pass a message to the serial port handler.
* `void Read()` is the thread that runs for the life of the application and interacts with the hardware device. Usually this consists of a synchronous or asynchronous loop waiting for input from the device.
* To pass messages back to the user call `parent.MsgSend(string msg)`.
