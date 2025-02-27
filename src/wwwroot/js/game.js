const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
const ws = new WebSocket(`${wsProtocol}//${window.location.host}/ws`);
const canvas = document.getElementById('gameCanvas');
const ctx = canvas.getContext('2d');
let playerId;
let gameState;

// Asset loading
const images = {
    dog: new Image(),
    human: new Image(),
    powerups: {
        SpeedBoost: new Image(),
        DoublePower: new Image(),
        Invincibility: new Image()
    }
};

images.dog.src = '/assets/sprites/dog.png';
images.human.src = '/assets/sprites/human.png';
images.powerups.SpeedBoost.src = '/assets/sprites/powerup.svg';
images.powerups.DoublePower.src = '/assets/sprites/powerup1.svg';
images.powerups.Invincibility.src = '/assets/sprites/powerup2.svg';

// WebSocket Connection Handlers
ws.onopen = () => {
    console.log('Connected to game server!');
    ws.send(JSON.stringify({
        action: 'join',
        name: 'Player' + Math.floor(Math.random() * 1000)
    }));
};

ws.onerror = (error) => {
    console.log('WebSocket error:', error);
};

ws.onclose = () => {
    console.log('Disconnected from server');
};

ws.onmessage = (event) => {
    console.log('Received game state:', event.data);
    gameState = JSON.parse(event.data);
    if (!playerId && Object.keys(gameState.Players).length > 0) {
        playerId = Object.keys(gameState.Players)[0];
        console.log('Player ID assigned:', playerId);
    }
    updateStats();
    draw();
};

function updateStats() {
    if (!gameState || !playerId) return;
    const player = gameState.Players[playerId];
    document.getElementById('level').textContent = player.Level;
    
    let powerups = [];
    if (player.IsInvincible) powerups.push('<span class="powerup powerup-invincible">Invincible</span>');
    if (player.HasSpeedBoost) powerups.push('<span class="powerup powerup-speed">Speed Boost</span>');
    if (player.HasDoublePower) powerups.push('<span class="powerup powerup-double">Double Power</span>');
    
    document.getElementById('powerups').innerHTML = powerups.length ? powerups.join(' ') : 'None';
}

function draw() {
    if (!gameState) return;
    
    // Clear canvas
    ctx.fillStyle = '#222';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Draw NPCs
    gameState.NPCs.forEach(npc => {
        ctx.fillStyle = '#0f0';
        ctx.beginPath();
        ctx.arc(npc.X, npc.Y, 15 + (npc.Size * 5), 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = '#fff';
        ctx.stroke();
    });

    // Draw PowerUps
    gameState.PowerUps.forEach(powerup => {
        const powerupSize = 20;
        ctx.fillStyle = getPowerUpColor(powerup.Type);
        ctx.beginPath();
        ctx.arc(powerup.X, powerup.Y, powerupSize, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = '#fff';
        ctx.stroke();
    });

    // Draw Players
    Object.values(gameState.Players).forEach(player => {
        const isCurrentPlayer = player.Id === playerId;
        drawPlayer(player, isCurrentPlayer);
    });
}

function drawPlayer(player, isCurrentPlayer) {
    const size = 30;
    ctx.save();
    
    // Player circle
    ctx.beginPath();
    ctx.arc(player.X, player.Y, size, 0, Math.PI * 2);
    ctx.fillStyle = player.IsInvincible ? '#ff0' : (isCurrentPlayer ? '#f00' : '#f66');
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Level text
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 16px Arial';
    ctx.textAlign = 'center';
    ctx.fillText(`Lv.${player.Level}`, player.X, player.Y - size - 5);

    // Name
    ctx.fillStyle = '#fff';
    ctx.font = '14px Arial';
    ctx.fillText(player.Name || 'Player', player.X, player.Y + size + 15);

    ctx.restore();
}

function getPowerUpColor(type) {
    switch(type) {
        case 'SpeedBoost': return '#FF4500';
        case 'DoublePower': return '#4B0082';
        case 'Invincibility': return '#008000';
        default: return '#00f';
    }
}

// Input Handlers
document.addEventListener('keydown', (e) => {
    if (!gameState || !playerId) return;
    const player = gameState.Players[playerId];
    let dx = 0, dy = 0;
    const speed = player.Speed;

    switch(e.key) {
        case 'ArrowUp': dy = -speed; break;
        case 'ArrowDown': dy = speed; break;
        case 'ArrowLeft': dx = -speed; break;
        case 'ArrowRight': dx = speed; break;
        case ' ':
            if (document.getElementById('mini-game').style.display === 'block') {
                ws.send(JSON.stringify({
                    action: 'miniGameInput',
                    playerId
                }));
            }
            break;
    }

    if (dx !== 0 || dy !== 0) {
        ws.send(JSON.stringify({
            action: 'move',
            x: player.X + dx,
            y: player.Y + dy
        }));
    }
});

canvas.addEventListener('click', (e) => {
    if (!gameState || !playerId) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    Object.entries(gameState.Players).forEach(([id, other]) => {
        if (id !== playerId) {
            const dx = other.X - x;
            const dy = other.Y - y;
            const distance = Math.sqrt(dx * dx + dy * dy);
            if (distance < 30) {
                ws.send(JSON.stringify({
                    action: 'attack',
                    targetId: id
                }));
            }
        }
    });
});

// Mini-game handling
function updateMiniGame(progress) {
    const progressBar = document.getElementById('battle-progress');
    progressBar.style.width = `${progress * 100}%`;
}

// Initial draw
draw();

// Animation loop
function gameLoop() {
    draw();
    requestAnimationFrame(gameLoop);
}

gameLoop();
