import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-health-page',
  imports: [CommonModule],
  template: `
    <main class="page">
      <header><p class="eyebrow">Live system status</p><h1>Service health</h1><p>Check the availability of the APIs powering this workspace.</p></header>
      <div class="panel">
        <div class="row"><span class="dot"></span><div><span>Order service</span><small>Orders API and database</small></div><strong>{{ orderHealth() }}</strong></div>
        <div class="row"><span class="dot"></span><div><span>Inventory service</span><small>Stock API, Redis and RabbitMQ consumer</small></div><strong>{{ inventoryHealth() }}</strong></div>
      </div>
    </main>
  `,
  styles: [`.page{display:grid;gap:1.35rem}header h1{margin:.2rem 0;font-size:clamp(1.85rem,6vw,2.55rem);letter-spacing:-.055em}header p:not(.eyebrow){margin:.35rem 0;color:var(--muted)}.eyebrow{margin:0;color:var(--primary);font-size:.7rem;font-weight:800;letter-spacing:.15em;text-transform:uppercase}.panel{display:grid;gap:.8rem;padding:1rem;border-radius:1.4rem;background:var(--surface);box-shadow:var(--raised)}.row{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:.8rem;padding:1rem;border-radius:1rem;box-shadow:var(--raised-sm)}.dot{width:.62rem;height:.62rem;border-radius:50%;background:var(--success);box-shadow:0 0 0 5px rgba(47,164,124,.13)}.row div{display:grid;gap:.2rem}.row div span{font-weight:800}.row small{color:var(--muted)}.row strong{color:var(--success);font-size:.9rem}`]
})
export class HealthPage implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly orderHealth = signal('Checking...');
  protected readonly inventoryHealth = signal('Checking...');

  ngOnInit(): void {
    this.api.getHealth('order').subscribe({
      next: health => this.orderHealth.set(health),
      error: () => this.orderHealth.set('Down')
    });
    this.api.getHealth('inventory').subscribe({
      next: health => this.inventoryHealth.set(health),
      error: () => this.inventoryHealth.set('Down')
    });
  }
}
