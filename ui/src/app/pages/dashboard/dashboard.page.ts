import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { InventoryItem } from '../../core/models/inventory.models';
import { OrderSummary } from '../../core/models/order.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [CommonModule, RouterLink],
  template: `
    <main class="shell">
      <section class="hero surface">
        <div>
          <p class="eyebrow">Command centre</p>
          <h1>Operations at a glance.</h1>
          <p class="lede">Monitor orders, inventory and service health from one calm workspace.</p>
        </div>
        <div class="actions">
          <a routerLink="/orders">Create order</a>
          <button type="button" (click)="refreshAll()" [disabled]="loading()">{{ loading() ? 'Refreshing...' : 'Refresh' }}</button>
        </div>
      </section>

      @if (errorMessage()) {
        <div class="error">{{ errorMessage() }}</div>
      }

      <section class="stats">
        <article class="card"><span class="icon">◌</span><div><span>Order service</span><strong>{{ orderHealth() }}</strong></div></article>
        <article class="card"><span class="icon success">◈</span><div><span>Inventory service</span><strong>{{ inventoryHealth() }}</strong></div></article>
        <article class="card"><span class="icon warm">▦</span><div><span>Products tracked</span><strong>{{ inventory().length }}</strong></div></article>
        <article class="card"><span class="icon violet">□</span><div><span>Total orders</span><strong>{{ orders().length }}</strong></div></article>
      </section>

      <section class="grid">
        <article class="panel surface">
          <div class="panel-title"><h2>Inventory snapshot</h2><a routerLink="/inventory">Manage stock</a></div>
          @for (item of inventory(); track item.productId) {
            <div class="row inventory-row"><strong>#{{ item.productId }}</strong><span>{{ item.name }}</span><span>{{ item.quantity }}</span></div>
          } @empty {
            <div class="empty">No inventory loaded.</div>
          }
        </article>

        <article class="panel surface">
          <div class="panel-title"><h2>Recent orders</h2><a routerLink="/orders">View all</a></div>
          @for (order of recentOrders(); track order.id) {
            <div class="row order-row"><strong>#{{ order.id }}</strong><span>Product {{ order.productId }} × {{ order.quantity }}</span><span>{{ order.status }}</span></div>
          } @empty {
            <div class="empty">No orders yet.</div>
          }
        </article>
      </section>
    </main>
  `,
  styles: [`
    .shell{display:grid;gap:1.35rem}.surface{border-radius:1.45rem;background:var(--surface);box-shadow:var(--raised)}.hero{display:grid;gap:1.2rem;padding:1.45rem}.hero h1{max-width:630px;margin:.25rem 0;font-size:clamp(1.85rem,6vw,3rem);letter-spacing:-.055em}.eyebrow{margin:0;color:var(--primary);font-size:.7rem;font-weight:800;letter-spacing:.15em;text-transform:uppercase}.lede,.empty{margin:.3rem 0 0;color:var(--muted);line-height:1.65}.actions{display:flex;flex-wrap:wrap;gap:.7rem}.actions a,.actions button,.panel-title a{border:0;border-radius:.85rem;padding:.76rem 1rem;font:inherit;font-size:.84rem;font-weight:800;text-decoration:none;cursor:pointer}.actions a{color:#fff;background:linear-gradient(145deg,#7483ef,#4b5bd5);box-shadow:4px 4px 8px #c3cad4,-4px -4px 8px #fff}.actions button,.panel-title a{color:var(--primary-dark);background:var(--surface);box-shadow:var(--raised-sm)}.actions button:active,.panel-title a:active{box-shadow:var(--pressed)}.actions button:disabled{opacity:.55}.stats{display:grid;gap:1rem}.card{display:flex;align-items:center;gap:.85rem;padding:1rem;border-radius:1.2rem;background:var(--surface);box-shadow:var(--raised-sm)}.card div{display:grid;gap:.2rem}.card div span{font-size:.76rem;color:var(--muted);font-weight:700}.card strong{font-size:1.2rem;letter-spacing:-.03em}.icon{display:grid;width:2.45rem;height:2.45rem;place-items:center;border-radius:.8rem;background:#e2e6ff;color:var(--primary);font-size:1.25rem}.icon.success{background:#dff5eb;color:var(--success)}.icon.warm{background:#fff0d9;color:#d88a38}.icon.violet{background:#eee4ff;color:#8458d2}.grid{display:grid;gap:1.35rem}.panel{padding:1.25rem}.panel-title{display:flex;align-items:center;justify-content:space-between;gap:1rem}.panel-title h2{margin:0;font-size:1.05rem}.panel-title a{padding:.55rem .7rem}.row{display:grid;gap:.45rem;padding:.9rem 0;border-bottom:1px solid var(--line);font-size:.9rem}.row strong:first-child{color:var(--primary-dark)}.inventory-row{grid-template-columns:.65fr 2fr .5fr}.order-row{grid-template-columns:.6fr 2fr 1fr}.order-row span:last-child{color:var(--success);font-weight:800}.error{padding:.85rem 1rem;border-radius:.9rem;color:#b84d5b;background:#f8e4e7;box-shadow:var(--pressed)}:host-context(.dark-theme) .icon{background:#2d3850}:host-context(.dark-theme) .icon.success{background:#1c453d}:host-context(.dark-theme) .icon.warm{background:#4a3822}:host-context(.dark-theme) .icon.violet{background:#372d50}:host-context(.dark-theme) .error{color:#ffb1ba;background:#462a35}@media(min-width:600px){.hero{grid-template-columns:1fr auto;align-items:end}.stats{grid-template-columns:repeat(2,1fr)}}@media(min-width:1080px){.stats{grid-template-columns:repeat(4,1fr)}.grid{grid-template-columns:1fr 1fr}} 
  `]
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly inventory = signal<InventoryItem[]>([]);
  protected readonly orders = signal<OrderSummary[]>([]);
  protected readonly orderHealth = signal('Checking...');
  protected readonly inventoryHealth = signal('Checking...');
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly recentOrders = () => this.orders().slice(0, 5);

  ngOnInit(): void {
    this.refreshAll();
  }

  protected refreshAll(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      inventory: this.api.getInventory(),
      orders: this.api.getOrders(),
      orderHealth: this.api.getHealth('order'),
      inventoryHealth: this.api.getHealth('inventory')
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => {
        this.inventory.set(result.inventory);
        this.orders.set([...result.orders].sort((left, right) => right.id - left.id));
        this.orderHealth.set(result.orderHealth);
        this.inventoryHealth.set(result.inventoryHealth);
      },
      error: () => this.errorMessage.set('Some dashboard data could not be loaded. Check the service health page.')
    });
  }
}
