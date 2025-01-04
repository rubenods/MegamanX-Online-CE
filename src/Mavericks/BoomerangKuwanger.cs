﻿using System;
using System.Collections.Generic;
using System.Linq;
using SFML.Graphics;

namespace MMXOnline;

public class BoomerangKuwanger : Maverick {
	public BoomerangKBoomerangWeapon boomerangWeapon = new();
	public BoomerangKDeadLiftWeapon deadLiftWeapon;
	public bool bald;
	public float dashSoundCooldown;
	public float teleportCooldown;

	public BoomerangKuwanger(Player player, Point pos, Point destPos, int xDir, ushort? netId, bool ownedByLocalPlayer, bool sendRpc = false) :
		base(player, pos, destPos, xDir, netId, ownedByLocalPlayer) {
		stateCooldowns.Add(typeof(MShoot), new MaverickStateCooldown(false, true, 0.75f));
		//stateCooldowns.Add(typeof(BoomerKDeadLiftState), new MaverickStateCooldown(false, true, 0.75f));
		deadLiftWeapon = new BoomerangKDeadLiftWeapon(player);
		gravityModifier = 1.25f;

		weapon = new Weapon(WeaponIds.BoomerangKGeneric, 97);

		awardWeaponId = WeaponIds.BoomerangCutter;
		weakWeaponId = WeaponIds.HomingTorpedo;
		weakMaverickWeaponId = WeaponIds.LaunchOctopus;

		netActorCreateId = NetActorCreateId.BoomerangKuwanger;
		netOwner = player;
		if (sendRpc) {
			createActorRpc(player.id);
		}

		usesAmmo = true;
		canHealAmmo = true;
		ammo = 32;
		maxAmmo = 32;
		grayAmmoLevel = 8;
		barIndexes = (58, 47);
	}

	public override void update() {
		base.update();
		Helpers.decrementTime(ref dashSoundCooldown);
		Helpers.decrementTime(ref teleportCooldown);

		if (state is not BoomerKTeleportState) {
			rechargeAmmo(4);
		}

		if (sprite.name == "boomerk_catch" && sprite.isAnimOver()) {
			changeSpriteFromName(state.sprite, true);
		}

		if (aiBehavior == MaverickAIBehavior.Control) {
			if (state is MIdle or MRun or MLand or BoomerKDashState) {
				if (state is not BoomerKDashState) {
					if (input.isHeld(Control.Left, player)) {
						xDir = -1;
						changeState(new BoomerKDashState(Control.Left));
						return;
					} else if (input.isHeld(Control.Right, player)) {
						xDir = 1;
						changeState(new BoomerKDashState(Control.Right));
						return;
					}
				}
				if (shootPressed() && !bald) {
					changeState(getShootState());
				} else if (specialPressed() && !bald) {
					changeState(new BoomerKDeadLiftState());
				} else if (player.dashPressed(out string dashControl) && teleportCooldown == 0 && !bald) {
					if (ammo >= 8) {
						deductAmmo(8);
						changeState(new BoomerKTeleportState());
					}
				}
			} else if (state is BoomerKTeleportState teleportState && teleportState.onceTeleportInSound) {
				if (specialPressed() && !bald) {
					changeState(new BoomerKDeadLiftState());
				}
			}
		} else {
			if (!bald && (state is MIdle or MRun or MLand or BoomerKDashState)) {
				foreach (var enemyPlayer in Global.level.players) {
					if (enemyPlayer.character == null || enemyPlayer == player) continue;
					var chr = enemyPlayer.character;
					if (!chr.canBeDamaged(player.alliance, player.id, null)) return;
					if (isFacing(chr) && getCenterPos().distanceTo(chr.getCenterPos()) < 10) {
						changeState(new BoomerKDeadLiftState());
					}
				}
			}
		}
	}

	public override float getRunSpeed() {
		return 175;
	}

	public override string getMaverickPrefix() {
		return "boomerk";
	}

	public MaverickState getShootState() {
		return new MShoot((Point pos, int xDir) => {
			bald = true;
			playSound("boomerkBoomerang", sendRpc: true);
			float inputAngle = 25;
			var inputDir = input.getInputDir(player);
			if (inputDir.x != 0 && inputDir.y == 0) inputAngle = 0;
			else if (inputDir.x != 0 && inputDir.y != 0) inputAngle = 30 * MathF.Sign(inputDir.y);
			else if (inputDir.x == 0 && inputDir.y != 0) inputAngle = 60 * MathF.Sign(inputDir.y);
			new BoomerangKBoomerangProj(boomerangWeapon, pos, xDir, this, inputAngle, player, player.getNextActorNetId(), sendRpc: true);
		}, null);
	}

	public override MaverickState[] aiAttackStates() {
		return new MaverickState[]
		{
				getShootState(),
				new BoomerKDeadLiftState(),
				new BoomerKTeleportState(),
		};
	}

	public override MaverickState getRandomAttackState() {
		var attacks = new MaverickState[]
		{
				getShootState(),
				new BoomerKTeleportState(),
		};
		return attacks.GetRandomItem();
	}

	// Melee IDs for attacks.
	public enum MeleeIds {
		None = -1,
		DeadLift,
	}

	// This can run on both owners and non-owners. So data used must be in sync.
	public override int getHitboxMeleeId(Collider hitbox) {
		return (int)(sprite.name switch {
			"boomerk_deadlift" => MeleeIds.DeadLift,
			_ => MeleeIds.None
		});
	}

	// This can be called from a RPC, so make sure there is no character conditionals here.
	public override Projectile? getMeleeProjById(int id, Point pos, bool addToLevel = true) {
		return (MeleeIds)id switch {
			MeleeIds.DeadLift => new GenericMeleeProj(
				deadLiftWeapon, pos, ProjIds.BoomerangKDeadLift, player,
				0, 0, 0, addToLevel: addToLevel
			),
			_ => null
		};
	}

	public void setTeleportCooldown() {
		teleportCooldown = 0.15f;
	}
}

#region weapons
public class BoomerangKBoomerangWeapon : Weapon {
	public BoomerangKBoomerangWeapon() {
		index = (int)WeaponIds.BoomerangKBoomerang;
		killFeedIndex = 97;
	}
}

public class BoomerangKDeadLiftWeapon : Weapon {
	public BoomerangKDeadLiftWeapon(Player player) {
		index = (int)WeaponIds.BoomerangKDeadLift;
		killFeedIndex = 97;
		damager = new Damager(player, 4, Global.defFlinch, 0.5f);
	}
}
#endregion

#region projectiles
public class BoomerangKBoomerangProj : Projectile {
	public float angleDist = 0;
	public float turnDir = 1;
	public Pickup pickup;
	public float maxSpeed = 400;
	float returnTime = 0.15f;
	public BoomerangKuwanger maverick;
	public BoomerangKBoomerangProj(
		Weapon weapon, Point pos, int xDir, BoomerangKuwanger maverick,
		float throwDirAngle, Player player, ushort netProjId, bool sendRpc = false
	) : base(
		weapon, pos, xDir, 250, 3, player, "boomerk_proj_horn",
		Global.defFlinch, 0.5f, netProjId, player.ownedByLocalPlayer
	) {
		projId = (int)ProjIds.BoomerangKBoomerang;
		angle = throwDirAngle;
		this.maverick = maverick;
		if (xDir == -1) angle = -180 - angle;
		destroyOnHit = false;
		shouldShieldBlock = false;
		shouldVortexSuck = false;

		if (sendRpc) {
			rpcCreate(pos, player, netProjId, xDir);
		}
	}

	public override void onCollision(CollideData other) {
		base.onCollision(other);
		if (!ownedByLocalPlayer) return;
		if (destroyed) return;

		if (other.gameObject is Pickup && pickup == null) {
			pickup = other.gameObject as Pickup;
			if (!pickup.ownedByLocalPlayer) {
				pickup.takeOwnership();
				RPC.clearOwnership.sendRpc(pickup.netId);
			}
		}

		var bk = other.gameObject as BoomerangKuwanger;
		if (time > returnTime && bk != null && bk.player == damager.owner) {
			if (pickup != null) {
				pickup.changePos(bk.pos);
			}
			bk.bald = false;
			bk.changeSpriteFromName("catch", true);
			destroySelf();
		}
	}

	public override void update() {
		base.update();
		if (!ownedByLocalPlayer) return;

		if (!destroyed && pickup != null) {
			pickup.collider.isTrigger = true;
			pickup.useGravity = false;
			pickup.changePos(pos);
		}

		if (time > returnTime) {
			if (angleDist < 180) {
				var angInc = (-xDir * turnDir) * Global.spf * maxSpeed;
				angle += angInc;
				angleDist += MathF.Abs(angInc);
				vel.x = Helpers.cosd((float)angle) * maxSpeed;
				vel.y = Helpers.sind((float)angle) * maxSpeed;
			} else if (maverick != null && !maverick.destroyed) {
				var dTo = pos.directionTo(maverick.getCenterPos()).normalize();
				var destAngle = MathF.Atan2(dTo.y, dTo.x) * 180 / MathF.PI;
				destAngle = Helpers.to360(destAngle);
				angle = Helpers.lerpAngle((float)angle, destAngle, Global.spf * 10);
			} else {
				destroySelf();
			}
		}
		vel.x = Helpers.cosd((float)angle) * maxSpeed;
		vel.y = Helpers.sind((float)angle) * maxSpeed;
	}

	public override void onDestroy() {
		base.onDestroy();
		if (pickup != null) {
			pickup.useGravity = true;
			pickup.collider.isTrigger = false;
		}
	}
}

#endregion

#region states

public class BoomerKTeleportState : MaverickState {
	public bool onceTeleportInSound;
	bool isInvisible;
	Actor clone;
	public BoomerKTeleportState() : base("teleport") {
		aiAttackCtrl = true;
	}

	public override void update() {
		base.update();

		if (!isInvisible && stateTime < 0.2f) {
			isInvisible = true;
			clone = new Actor("empty", maverick.pos, null, true, false);
			var rect = new Rect(0, 0, maverick.width, maverick.height);
			clone.spriteToCollider["teleport"] = new Collider(rect.getPoints(), false, clone, false, false, 0, new Point(0, 0));
			clone.changeSprite("boomerk_teleport", false);
			clone.alpha = 0.5f;
			clone.xDir = maverick.xDir;
			clone.visible = false;
			clone.useGravity = false;
			maverick.useGravity = false;
		}
		if (isInvisible && stateTime > 0.4f) {
			isInvisible = false;
			if (canChangePos()) {
				Point? prevCamPos = null;
				if (player.character != null) {
					prevCamPos = player.character.getCamCenterPos();
					player.character.stopCamUpdate = true;
				}
				maverick.changePos(clone.pos);
				if (prevCamPos != null && player.isTagTeam()) {
					Global.level.snapCamPos(player.character.getCamCenterPos(), prevCamPos);
				}
			}
			clone.destroySelf();
			clone = null;
		}

		if (isInvisible) {
			var dir = input.getInputDir(player);
			float moveAmount = dir.x * 300 * Global.spf;

			var hitWall = Global.level.checkTerrainCollisionOnce(clone, moveAmount, -2);
			if (hitWall != null && hitWall.getNormalSafe().y == 0) {
				float rectW = hitWall.otherCollider.shape.getRect().w();
				if (rectW < 75) {
					float wallClipAmount = moveAmount + dir.x * (rectW + maverick.width);
					var hitWall2 = Global.level.checkTerrainCollisionOnce(clone, wallClipAmount, -2);
					if (hitWall2 == null && clone.pos.x + wallClipAmount > 0 && clone.pos.x + wallClipAmount < Global.level.width) {
						clone.incPos(new Point(wallClipAmount, 0));
						clone.visible = true;
					}
				}
			} else {
				if (MathF.Abs(moveAmount) > 0) clone.visible = true;
				clone.move(new Point(moveAmount, 0), useDeltaTime: false);
			}

			if (!canChangePos()) {
				var hits = Global.level.raycastAllSorted(clone.getCenterPos(), clone.getCenterPos().addxy(0, 200), new List<Type> { typeof(Wall) });
				var hit = hits.FirstOrDefault();
				if (hit != null) {
					clone.visible = true;
					clone.changePos(hit.getHitPointSafe());
				}
			}

			if (!canChangePos()) {
				var redXPos = clone.getCenterPos();
				DrawWrappers.DrawLine(redXPos.x - 10, redXPos.y - 10, redXPos.x + 10, redXPos.y + 10, Color.Red, 2, ZIndex.HUD);
				DrawWrappers.DrawLine(redXPos.x - 10, redXPos.y + 10, redXPos.x + 10, redXPos.y - 10, Color.Red, 2, ZIndex.HUD);
			}
		}

		if (stateTime < 0.25f) {
			maverick.visible = Global.isOnFrameCycle(5);
		} else if (stateTime >= 0.2f && stateTime < 0.4f) {
			maverick.visible = false;
		} else if (stateTime > 0.6f) {
			if (!onceTeleportInSound) {
				onceTeleportInSound = true;
				maverick.playSound("boomerkTeleport", sendRpc: true);
			}
			maverick.visible = Global.isOnFrameCycle(5);
		}

		if (stateTime > 0.8f) {
			maverick.changeState(new MIdle());
		}
	}

	public override void onEnter(MaverickState oldState) {
		base.onEnter(oldState);
		maverick.playSound("boomerkTeleport", sendRpc: true);
	}

	public override void onExit(MaverickState newState) {
		base.onExit(newState);
		maverick.visible = true;
		maverick.useGravity = true;
		if (maverick is BoomerangKuwanger bk) {
			bk.setTeleportCooldown();
		}
		if (clone != null) {
			clone.destroySelf();
		}
	}

	public bool canChangePos() {
		if (Global.level.checkTerrainCollisionOnce(clone, 0, 5) == null) return false;
		var hits = Global.level.getTerrainTriggerList(clone, new Point(0, 5), typeof(KillZone));
		if (hits.Count > 0) return false;
		return true;
	}
}

public class BoomerKDashState : MaverickState {
	public float dashTime = 0;
	public string initialDashButton;

	public BoomerKDashState(string initialDashButton) : base("dash") {
		this.initialDashButton = initialDashButton;
		normalCtrl = true;
		attackCtrl = true;
		aiAttackCtrl = true;
	}

	public override void onEnter(MaverickState oldState) {
		base.onEnter(oldState);
		if (maverick is BoomerangKuwanger bk && bk.dashSoundCooldown == 0) {
			maverick.playSound("boomerkDash", sendRpc: true);
			bk.dashSoundCooldown = 0.25f;
		}
	}

	public override void onExit(MaverickState newState) {
		base.onExit(newState);
	}

	public override void update() {
		base.update();
		groundCode();

		dashTime += Global.spf;
		float modifier = 1;

		if (!input.isHeld(initialDashButton, player)) {
			maverick.changeState(new MIdle());
			return;
		}

		var move = new Point(0, 0);
		move.x = 300 * maverick.xDir * modifier;
		maverick.move(move);
	}
}

public class BoomerKDeadLiftState : MaverickState {
	private Character grabbedChar;
	float timeWaiting;
	bool grabbedOnce;
	public BoomerKDeadLiftState() : base("deadlift") {
	}

	public override void onEnter(MaverickState oldState) {
		base.onEnter(oldState);
	}

	public override void update() {
		base.update();

		if (!grabbedOnce && grabbedChar != null && !grabbedChar.sprite.name.EndsWith("_grabbed") && maverick.frameIndex > 1 && timeWaiting < 0.5f) {
			maverick.frameSpeed = 0;
			timeWaiting += Global.spf;
		} else {
			maverick.frameSpeed = 1;
		}

		if (grabbedChar != null && grabbedChar.sprite.name.EndsWith("_grabbed")) {
			grabbedOnce = true;
		}

		if (maverick.isAnimOver()) {
			maverick.changeState(new MIdle());
		}
	}

	public override bool trySetGrabVictim(Character grabbed) {
		if (grabbedChar == null) {
			grabbedChar = grabbed;
			return true;
		}
		return false;
	}
}

public class DeadLiftGrabbed : GenericGrabbedState {
	public Character? grabbedChar;
	public bool launched;
	float launchTime;
	public DeadLiftGrabbed(BoomerangKuwanger grabber) : base(grabber, 1, "") {
		customUpdate = true;
	}

	public override void update() {
		base.update();
		if (!character.ownedByLocalPlayer) { return; }

		if (launched) {
			launchTime += Global.spf;
			if (launchTime > 0.33f) {
				character.changeToIdleOrFall();
				return;
			}
			if (Global.level.checkTerrainCollisionOnce(character, 0, -1) != null) {
				new BoomerangKDeadLiftWeapon((grabber as Maverick).player).applyDamage(character, false, character, (int)ProjIds.BoomerangKDeadLift);
				character.playSound("crash", sendRpc: true);
				character.shakeCamera(sendRpc: true);
			}
			return;
		}

		if (grabber.sprite?.name.EndsWith("_deadlift") == true) {
			if (grabber.frameIndex < 4) {
				trySnapToGrabPoint(true);
			} else if (!launched) {
				launched = true;
				character.unstickFromGround();
				character.vel.y = -600;
			}
		} else {
			notGrabbedTime += Global.spf;
		}

		if (notGrabbedTime > 0.5f) {
			character.changeToIdleOrFall();
		}
	}
}
#endregion
