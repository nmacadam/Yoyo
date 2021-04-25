# Yoyo
Yoyo is a client-server network engine for Unity.  It features TCP packet delivery, direct to byte-array packet serialization/deserialization, and a set of Unity components that allow for quickly setting up a networked game environment.  This repository was created for an academic project, is no longer actively maintained, and not recommended for production use.  It is however, a good reference for implementing critical networking functionality.

## Quick Start
Follow the below installation instructions to import the Yoyo package into your Unity project.  
There are 3 main component classes to be aware of with Yoyo: YoyoSession, NetworkEntity, and NetworkBehaviour

YoyoSession
- Represents the client/server as a whole
- Manages the socket and sending messages via TCP
- Manages setup/teardown for networking lifecycle
- Should only have 1 instance in a scene

NetworkEntity
- Represents a networked GameObject
- Owns a collection of NetworkBehaviours and updates them
- Holds an identifier for the GameObject it represents

NetworkBehaviour
- The networked MonoBehaviour equivalent
- Starting point for sending a packet

### Example
We'll build a quick application that synchronizes an object's position on the client with the position on the server (no interpolation or anything yet!).

Start by adding a YoyoSession to the scene.  For now, we'll leave the default settings.
Create a C# script called `SyncPosition.cs`, that inherits from `NetworkBehaviour`

```
public class SyncPosition : NetworkBehaviour
{
  protected override void NetUpdate()
  {
    if (IsServer)
    {
      // An update packet is one the server sends to the client
      // (The opposite of this is a 'command packet')
      // This just writes some necessary packet info automatically
      Packet packet = GetUpdatePacket();
      
      // Write the position to the packet; The Write method is overloaded to accept a 
      // number of data types
      packet.Write(transform.position);
      
      // Send the packet;  The packet will be sent out
      // during the next YoyoSession update cycle
      SendUpdate(packet);
    }
  }
  
  public override void HandleMessage(Packet packet)
  {
    // We've recieved a packet that was sent by this component in a different network application
    
    if (IsClient)
    {
      // IsClient will be true for all clients, not just the local client (for that, use IsLocalPlayer)
    
      // Since this component only creates the above position update packet, we can assume the packet will contain
      // a Vector3
      Vector3 receivedPosition = packet.ReadVector3();
      transform.position = receivedPosition;
    }
  }
}
```

Now create a GameObject, add the NetworkEntity component, and add the SyncPosition component.  The NetworkEntity component will automatically update the NetworkBehaviour's ID and Entity reference field.  Create a prefab of this object.

Now, create a NetworkContract scriptable object by right-clicking in the Project window, and selecting 'Yoyo->Network Contract'.  The NetworkContract is a list of prefabs that can be synchronously instantiated across the network.  Add the previously created prefab to the NetworkContract's prefab list.  Notice the 0 index is highlighted.  This is because this prefab is the 'Default Network Object,' meaning it will be automatically instantiated for each new connection.  Use it to represent a player, or a player-manager object that then instantiates a player.  Add the Network Contract you created to the YoyoSession's Network Contract field.

Finally, we need a way to start either the client or the server in a build.  Create a canvas with a 'Client' button and 'Server' button.  Map the UnityEvent for each of these to the YoyoSession's 'StartClient' and 'StartServer' respectively.

With that, you can build the application, running the client in build, and the server in editor. Drag the networked object around in the server, and it should also move in the client!

To synchronize the whole transform, write and read additional values from the packet (rotation, scale...).  To move the object in the client (like a typical game), switch the IsClient/IsServer booleans, and replace `GetUpdatePacket` and `SendUpdate` with `GetCommandPacket` and `SendCommand` respectively.  Note, the server will additionally have to pass the packet through to other clients if you want the position to be synchronized across all game instances.

## Installation
### Git
This package can be installed with the Unity Package Manager by selecting the add package dropdown, clicking "Add package from git url...", and entering `https://github.com/nmacadam/Yoyo.git`.

Alternatively the package can be added directly to the Unity project's manifest.json by adding the following line:
```C#
{
  "dependencies": {
      ...
      "com.daruma-works.yoyo":"https://github.com/nmacadam/Yoyo.git"
      ...
  }
}
```
For either option, by appending `#<release>` to the Oni.git url you can specify a specific release (e.g. Oni.git#1.0.0-preview)

### Manual
Download this repository as a .zip file and extract it, open the Unity Package Manager window, and select "Add package from disk...".  Then select the package.json in the extracted folder.

## Limitations
- No UDP
- Scene switching is unsupported
- No way to immediately send a message; all messages are passed through a queue system that is processed at the frequency of the session's master timer
- Clients can only send messages to the server
- Server messages are always sent to all clients
