# DOTS Shooter

This game is a simple autoshooter, implemented using [DOTS](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/index.html).

The Ui is build using [UIToolkit](https://docs.unity3d.com/Manual/UIElements.html).

The monobehavior side is kept decoupled using Event Based SOAP architecture using [UnityAtoms](https://github.com/unity-atoms/unity-atoms)

We introduced liveops through [Metaplay](https://metaplay.io/)

## Gameplay

The gameplay is simple. The player moves with WASD, dodging enemies while the avatar automatically shoots the nearest enemy. The player can collect stars from dead enemies, increasing their gold.

Each round the player has to survive for a set inteval, after which they are presented with a shop where they can spend the collected gold to boost their skills, increasings survivability.

## Art

The artstyle is claymation made by Andreas Nøhr


## Metaplay

Metaplay is introduced facilitate various liveops features. The players gold and skills are configured and tracked on the server.

### Shortcomings and cheat-proofing.

Currently we don't simulate the game on an authorative server. This means that a hacked client could potentially send false `PlayerAddGold` actions to the server.

The since we let the clients simulate the game, we have to find other ways to cheat-proof.

An initialy step would be to ensure that we only accept `PlayerAddGold` action when the player is in an active session. IE not in the shop. Lets call it `InArena` Then we need to introduce an action to track this. Since our Arena sessions are time based the server could then also verify that the player is only `InArena` in that span of time.

Since our enemy spawning is Time based we have a theoretical Max amount of gold collectable in each Arena session, which we also could use to verify the player doesn't send too many AddGold actions.

The best solution would be to have a more server authorative setup, but such a solution would potentially be more expensive in hosting etc.