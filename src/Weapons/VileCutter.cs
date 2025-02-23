﻿using System;
using System.Collections.Generic;

namespace MMXOnline;

public enum VileCutterType {
	None = -1,
	QuickHomesick,
	ParasiteSword,
	MaroonedTomahawk
}

public class VileCutter : Weapon {
	public float vileAmmoUsage;
	public string projSprite;
	public string fadeSprite;
	public ProjIds projId;
	public VileCutter(VileCutterType vileCutterType) : base() {
		index = (int)WeaponIds.VileCutter;
		type = (int)vileCutterType;
		fireRate = 60;

		if (vileCutterType == VileCutterType.None) {
			displayName = "None(MISSILE)";
			description = new string[] { "Do not equip a Cutter.", "MISSILE will be used instead." };
			killFeedIndex = 126;
		}
		if (vileCutterType == VileCutterType.QuickHomesick) {
			displayName = "Quick Homesick";
			projId = ProjIds.QuickHomesick;
			projSprite = "cutter_qh";
			vileAmmoUsage = 8;
			description = new string[] { "This cutter travels in an arc like a", "boomerang. Use it to pick up items!" };
			killFeedIndex = 114;
			vileWeight = 3;
			ammousage = vileAmmoUsage;
			damage = "2";
			hitcooldown = "0.5";
			effect = "Can carry items.";
		} else if (vileCutterType == VileCutterType.ParasiteSword) {
			displayName = "Parasite Sword";
			projId = ProjIds.ParasiteSword;
			projSprite = "cutter_ps";
			vileAmmoUsage = 8;
			description = new string[] { "Fires cutters that grow as they fly", "and can pierce enemies." };
			killFeedIndex = 115;
			vileWeight = 3;
			ammousage = vileAmmoUsage;
			damage = "2";
			hitcooldown = "0.5";
			effect = "Won't Destroy on hit.";
		} else if (vileCutterType == VileCutterType.MaroonedTomahawk) {
			displayName = "Marooned Tomahawk";
			projId = ProjIds.MaroonedTomahawk;
			projSprite = "cutter_mt";
			vileAmmoUsage = 16;
			description = new string[] { "This long-lasting weapon spins", "in place and goes through objects." };
			killFeedIndex = 116;
			vileWeight = 3;
			ammousage = vileAmmoUsage;
			damage = "1";
			hitcooldown = "0.33";
			effect = "Won't Destroy on hit.";
		}
	}

	public override float getAmmoUsage(int chargeLevel) {
		return vileAmmoUsage;
	}

	public override void vileShoot(WeaponIds weaponInput, Vile vile) {
		if (shootCooldown == 0) {
			if (vile.tryUseVileAmmo(vileAmmoUsage)) {
				vile.setVileShootTime(this);
				if (!vile.grounded) {
					vile.changeState(new CutterAttackState(grounded: false), true);
				} else {
					vile.changeState(new CutterAttackState(grounded: true), true);
				}
			}
		}
	}
}

public class CutterAttackState : CharState {
	VileCutterProj proj;

	public CutterAttackState(bool grounded) : base(getSprite(grounded)) {
		exitOnAirborne = true;
		normalCtrl = true;
	}
	public static string getSprite(bool grounded) {
		return grounded ? "idle_shoot" : "cannon_air";
	}

	public override void update() {
		base.update();

		groundCodeWithMove();

		if (proj != null && !player.input.isHeld(Control.Special1, player)) {
			proj.maroon();
		}

		if (character.sprite.isAnimOver()) {
			character.changeToIdleOrFall();
		}
	}

	public void shootLogic(Vile vile) {
		if (vile.sprite.getCurrentFrame().POIs.IsNullOrEmpty()) return;
		Point shootVel = vile.getVileShootVel(true);
		var poi = vile.sprite.getCurrentFrame().POIs[0];
		poi.x *= vile.xDir;
		var player = vile.player;
		Point muzzlePos = vile.pos.add(poi);
		vile.playSound("frontrunner", sendRpc: true);

		proj = new VileCutterProj(vile.cutterWeapon, muzzlePos, vile.getShootXDir(), player, player.getNextActorNetId(), shootVel, rpc: true);
	}

	public override void onEnter(CharState oldState) {
		base.onEnter(oldState);
		shootLogic(character as Vile);
	}

	public override void onExit(CharState newState) {
		base.onExit(newState);
		proj?.maroon();
	}
}

public class VileCutterProj : Projectile {
	public float angleDist = 0;
	public float turnDir = 1;
	public Pickup pickup;
	public float angle2;

	public float maxSpeed = 350;
	public float returnTime = 0.15f;
	public float turnSpeed = 300;
	public float maxAngleDist = 180;
	public VileCutterType vileCutterType;
	public float soundCooldown;

	public VileCutterProj(VileCutter weapon, Point pos, int xDir, Player player, ushort netProjId, Point? vel = null, bool rpc = false) :
		base(weapon, pos, xDir, 350, 2, player, weapon.projSprite, 0, 0.5f, netProjId, player.ownedByLocalPlayer) {
		fadeSprite = weapon.fadeSprite;
		projId = (int)weapon.projId;
		destroyOnHit = true;
		vileCutterType = (VileCutterType)weapon.type;
		if (vileCutterType == VileCutterType.ParasiteSword) {
			destroyOnHit = false;
			maxAngleDist = 45;
			returnTime = 0;
			globalCollider = new Collider(new Rect(0, 0, 19, 19).getPoints(), true, this, false, false, 0, Point.zero);
		} else if (vileCutterType == VileCutterType.MaroonedTomahawk) {
			destroyOnHit = false;
			maxAngleDist = 45;
			returnTime = 0;
			damager.damage = 1;
			damager.hitCooldown = 20;
		}

		this.vel.y = 50;
		angle2 = 0;
		if (xDir == -1) angle2 = -180;

		if (rpc) {
			rpcCreate(pos, player, netProjId, xDir);
		}
		canBeLocal = false;
	}

	public override void onCollision(CollideData other) {
		base.onCollision(other);
		if (!ownedByLocalPlayer) return;
		if (vileCutterType != VileCutterType.QuickHomesick) return;

		if (other.gameObject is Pickup && pickup == null) {
			pickup = other.gameObject as Pickup;
			if (!pickup.ownedByLocalPlayer) {
				pickup.takeOwnership();
				RPC.clearOwnership.sendRpc(pickup.netId);
			}
		}

		if (time > returnTime && other.gameObject is Character character && character.player == damager.owner) {
			if (pickup != null) {
				pickup.changePos(character.getCenterPos());
			}
			destroySelf();
			character.player.vileAmmo = Helpers.clampMax(character.player.vileAmmo + 8, character.player.vileMaxAmmo);
		}
	}

	public override void onDestroy() {
		base.onDestroy();
		if (pickup != null) {
			pickup.useGravity = true;
			pickup.collider.isTrigger = false;
		}
	}

	public void maroon() {
		if (vileCutterType == VileCutterType.MaroonedTomahawk) {
			time = returnTime;
			angleDist = maxAngleDist;
		}
	}

	public override void update() {
		base.update();

		if (!destroyed && pickup != null) {
			pickup.collider.isTrigger = true;
			pickup.useGravity = false;
			pickup.changePos(pos);
		}

		soundCooldown -= Global.spf;
		if (soundCooldown <= 0) {
			soundCooldown = 0.3f;
			playSound("cutter", sendRpc: true);
		}

		if (vileCutterType == VileCutterType.ParasiteSword) {
			if (xScale < 2) {
				xScale += Global.spf * 2;
				yScale += Global.spf * 2;
				float factor = 18;

				changeGlobalCollider(new List<Point>
				{
						globalCollider._shape.points[0],
						globalCollider._shape.points[1].addxy(Global.spf * factor, 0),
						globalCollider._shape.points[2].addxy(Global.spf * factor, Global.spf * factor),
						globalCollider._shape.points[3].addxy(0, Global.spf * factor),
					});
			}
		}

		if (time > returnTime) {
			if (angleDist < maxAngleDist) {
				var angInc = (-xDir * turnDir) * Global.spf * turnSpeed;
				angle2 += angInc;
				angleDist += MathF.Abs(angInc);
				vel.x = Helpers.cosd(angle2) * maxSpeed;
				vel.y = Helpers.sind(angle2) * maxSpeed;
			} else if (vileCutterType == VileCutterType.MaroonedTomahawk) {
				maxTime = 3;
				vel = Point.zero;
			} else if (vileCutterType == VileCutterType.ParasiteSword) {
				maxTime = 1;
			} else if (damager.owner.character != null) {
				var dTo = pos.directionTo(damager.owner.character.getCenterPos()).normalize();
				var destAngle = MathF.Atan2(dTo.y, dTo.x) * 180 / MathF.PI;
				destAngle = Helpers.to360(destAngle);
				angle2 = Helpers.lerpAngle(angle2, destAngle, Global.spf * 10);
				vel.x = Helpers.cosd(angle2) * maxSpeed;
				vel.y = Helpers.sind(angle2) * maxSpeed;
			} else {
				destroySelf();
			}
		}
	}
}
