const powerUpImages = {
    SpeedBoost: new Image(),
    DoublePower: new Image(),
    Invincibility: new Image()
};

powerUpImages.SpeedBoost.src = '/assets/sprites/powerup.svg';
powerUpImages.DoublePower.src = '/assets/sprites/powerup1.svg';
powerUpImages.Invincibility.src = '/assets/sprites/powerup2.svg';

function drawPowerUps() {
    gameState.PowerUps.forEach(powerup => {
        const img = powerUpImages[powerup.Type];
        ctx.drawImage(img, powerup.X - 32, powerup.Y - 32, 64, 64);
    });
}
