const SOUNDS = {
    attack: new Audio('/assets/sounds/attack.mp3'),
    powerup: new Audio('/assets/sounds/powerup.mp3'),
    levelup: new Audio('/assets/sounds/levelup.mp3')
};

Object.values(SOUNDS).forEach(sound => {
    sound.volume = 0.3;
});

// Particle System
class ParticleSystem {
    constructor() {
        this.particles = [];
    }

    emit(x, y, color, count = 10) {
        for (let i = 0; i < count; i++) {
            this.particles.push({
                x, y,
                vx: (Math.random() - 0.5) * 10,
                vy: (Math.random() - 0.5) * 10,
                life: 1,
                color
            });
        }
    }

    update() {
        this.particles = this.particles.filter(p => {
            p.x += p.vx;
            p.y += p.vy;
            p.life -= 0.02;
            return p.life > 0;
        });
    }

    draw(ctx) {
        this.particles.forEach(p => {
            ctx.globalAlpha = p.life;
            ctx.fillStyle = p.color;
            ctx.beginPath();
            ctx.arc(p.x, p.y, 5, 0, Math.PI * 2);
            ctx.fill();
        });
        ctx.globalAlpha = 1;
    }
}

// UI System
class GameUI {
    static updateScoreboard(players) {
        const scores = Object.values(players)
            .sort((a, b) => b.Level - a.Level)
            .slice(0, 3);
        
        const scoreHtml = scores.map((player, index) => `
            <div class="score-entry">
                <span>${index + 1}. ${player.Name}</span>
                <span>Level ${player.Level}</span>
            </div>
        `).join('');
        
        document.getElementById('topScores').innerHTML = scoreHtml;
    }

    static showAttackRange(x, y) {
        const indicator = document.getElementById('attackIndicator');
        indicator.style.display = 'block';
        indicator.style.left = `${x - 50}px`;
        indicator.style.top = `${y - 50}px`;
        indicator.style.width = '100px';
        indicator.style.height = '100px';
        setTimeout(() => indicator.style.display = 'none', 100);
    }
}

// Main Game Code
const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
const ws = new WebSocket(`${wsProtocol}//${window.location.host}/ws`);
const canvas = document.getElementById('gameCanvas');
const ctx = canvas.getContext('2d');
let playerId;
let gameState;

const particles = new ParticleSystem();

// Game assets
const ASSETS = {
    dog: '/assets/sprites/dog.svg',
    human: '/assets/sprites/human.svg',
    powerups: {
        SpeedBoost: '/assets/sprites/powerup.svg',
        DoublePower: '/assets/sprites/powerup1.svg',
        Invincibility: '/assets/sprites/powerup2.svg'
    }
};

// Wall configuration
const WALLS = [
    { x: 100, y: 100, width: 200, height: 40 },
    { x: 400, y: 300, width: 40, height: 400 },
    { x: 800, y: 200, width: 300, height: 40 },
    { x: 1200, y: 600, width: 40, height: 300 },
    { x: 200, y: 800, width: 400, height: 40 }
];

// Game constants
const PLAYER_SIZE = 15;
const BASE_SPEED = 8;
const ATTACK_RANGE = 50;

function loadAssets() {
    return new Promise(resolve => {
        let loaded = 0;
        const required = Object.keys(ASSETS).length + Object.keys(ASSETS.powerups).length;
        
        function onLoad() {
            loaded++;
            if (loaded === required) resolve();
        }

        Object.entries(ASSETS).forEach(([key, value]) => {
            if (typeof value === 'string') {
                const img = new Image();
                img.onload = onLoad;
                img.src = value;
                ASSETS[key] = img;
            } else {
                Object.entries(value).forEach(([pKey, pValue]) => {
                    const img = new Image();
                    img.onload = onLoad;
                    img.src = pValue;
                    ASSETS[key][pKey] = img;
                });
            }
        });
    });
}

ws.onopen = () => {
    console.log('Connected to game server!');
    ws.send(JSON.stringify({
        action: 'join',
        name: playerName
    }));
};

ws.onmessage = (event) => {
    gameState = JSON.parse(event.data);
    if (!playerId && Object.keys(gameState.Players).length > 0) {
        playerId = Object.keys(gameState.Players)[0];
    }
    GameUI.updateScoreboard(gameState.Players);
};

function drawWalls() {
    ctx.fillStyle = '#444';
    ctx.strokeStyle = '#666';
    ctx.lineWidth = 2;
    WALLS.forEach(wall => {
        ctx.fillRect(wall.x, wall.y, wall.width, wall.height);
        ctx.strokeRect(wall.x, wall.y, wall.width, wall.height);
    });
}

function checkWallCollision(x, y, radius = PLAYER_SIZE) {
    return WALLS.some(wall => 
        x + radius > wall.x && 
        x - radius < wall.x + wall.width && 
        y + radius > wall.y && 
        y - radius < wall.y + wall.height
    );
}

function draw() {
    if (!gameState) return;

    ctx.fillStyle = '#1a1a1a';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    drawWalls();

    // Draw game elements
    gameState.PowerUps.forEach(drawPowerUp);
    gameState.NPCs.forEach(drawNPC);
    Object.values(gameState.Players).forEach(player => 
        drawPlayer(player, player.Id === playerId)
    );

    particles.update();
    particles.draw(ctx);
}

function drawPlayer(player, isCurrentPlayer) {
    ctx.save();
    ctx.translate(player.X, player.Y);

    // Draw player body
    ctx.beginPath();
    ctx.arc(0, 0, PLAYER_SIZE, 0, Math.PI * 2);
    ctx.fillStyle = player.IsInvincible ? '#ff0' : (isCurrentPlayer ? '#f00' : '#f66');
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.stroke();

    // Draw player name and level
    ctx.fillStyle = '#fff';
    ctx.textAlign = 'center';
    ctx.font = 'bold 14px Arial';
    ctx.fillText(player.Name, 0, -PLAYER_SIZE - 10);
    ctx.font = '12px Arial';
    ctx.fillText(`Lv.${player.Level}`, 0, -PLAYER_SIZE - 25);

    ctx.restore();
}

function drawPowerUp(powerup) {
    const img = ASSETS.powerups[powerup.Type];
    if (img) {
        ctx.drawImage(img, powerup.X - 15, powerup.Y - 15, 30, 30);
    }
}

function drawNPC(npc) {
    ctx.fillStyle = '#0f0';
    ctx.beginPath();
    ctx.arc(npc.X, npc.Y, 15 + (npc.Size * 5), 0, Math.PI * 2);
    ctx.fill();
}

canvas.addEventListener('click', (e) => {
    if (!gameState || !playerId) return;
    const rect = canvas.getBoundingClientRect();
    const scale = canvas.width / rect.width;
    const x = (e.clientX - rect.left) * scale;
    const y = (e.clientY - rect.top) * scale;

    Object.entries(gameState.Players).forEach(([id, other]) => {
        if (id !== playerId) {
            const dx = other.X - x;
            const dy = other.Y - y;
            const distance = Math.sqrt(dx * dx + dy * dy);
            if (distance < ATTACK_RANGE) {
                ws.send(JSON.stringify({
                    action: 'attack',
                    targetId: id
                }));
                SOUNDS.attack.play();
                particles.emit(x, y, '#f00', 20);
                GameUI.showAttackRange(e.clientX, e.clientY);
            }
        }
    });
});

document.addEventListener('keydown', (e) => {
    if (!gameState || !playerId) return;
    const player = gameState.Players[playerId];
    let dx = 0, dy = 0;
    const speed = BASE_SPEED * (player.HasSpeedBoost ? 2 : 1);

    switch(e.key) {
        case 'ArrowUp': dy = -speed; break;
        case 'ArrowDown': dy = speed; break;
        case 'ArrowLeft': dx = -speed; break;
        case 'ArrowRight': dx = speed; break;
    }

    if (dx !== 0 || dy !== 0) {
        const newX = player.X + dx;
        const newY = player.Y + dy;
        
        if (!checkWallCollision(newX, newY)) {
            ws.send(JSON.stringify({
                action: 'move',
                x: newX,
                y: newY
            }));
        }
    }
});

// Start game loop
loadAssets().then(() => {
    function gameLoop() {
        draw();
        requestAnimationFrame(gameLoop);
    }
    gameLoop();
});
