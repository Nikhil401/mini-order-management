import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { OrderSummary } from '../../core/models/order.models';

@Component({
  selector: 'app-orders-page',
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <main class="page">
      <header class="heading">
        <div>
          <p class="eyebrow">Order management</p>
          <h1>Orders</h1>
          <p class="subtitle">Create an order and follow its persisted status.</p>
        </div>
        <button type="button" class="secondary" (click)="loadOrders()" [disabled]="loading()">Refresh</button>
      </header>

      @if (!auth.accessToken()) {
        <section class="auth-card">
          <div>
            <h2>Admin sign in</h2>
            <p>Creating orders is protected by role-based authorization.</p>
            <small>Learning account: admin / admin123</small>
          </div>
          <form [formGroup]="loginForm" (ngSubmit)="login()">
            <input type="text" formControlName="username" autocomplete="username" placeholder="Username" />
            <input type="password" formControlName="password" autocomplete="current-password" placeholder="Password" />
            <button type="submit" [disabled]="loginForm.invalid || signingIn()">
              {{ signingIn() ? 'Signing in...' : 'Sign in' }}
            </button>
          </form>
        </section>
      } @else {
        <section class="create-card">
          <form [formGroup]="orderForm" (ngSubmit)="createOrder()">
            <label>
              <span>Product ID</span>
              <input type="number" min="1" formControlName="productId" />
            </label>
            <label>
              <span>Quantity</span>
              <input type="number" min="1" formControlName="quantity" />
            </label>
            <button type="submit" [disabled]="orderForm.invalid || submitting()">
              {{ submitting() ? 'Creating...' : 'Create order' }}
            </button>
            <button type="button" class="secondary" (click)="logout()">Sign out</button>
          </form>
        </section>
      }

      @if (message()) {
        <div class="message" [class.error]="hasError()">{{ message() }}</div>
      }

      <section class="panel">
        <div class="table-header"><span>Order</span><span>Product</span><span>Quantity</span><span>Status</span><span>Created</span></div>
        @for (order of orders(); track order.id) {
          <div class="row">
            <strong>#{{ order.id }}</strong>
            <span>#{{ order.productId }}</span>
            <span>{{ order.quantity }}</span>
            <strong class="status">{{ order.status }}</strong>
            <small>{{ order.createdAtUtc | date : 'medium' }}</small>
          </div>
        } @empty {
          <div class="empty">{{ loading() ? 'Loading orders...' : 'No orders yet.' }}</div>
        }
      </section>
    </main>
  `,
  styles: [`
    .page{display:grid;gap:1.35rem}.heading{display:flex;justify-content:space-between;gap:1rem;align-items:end}.heading h1{margin:.2rem 0;font-size:clamp(1.85rem,6vw,2.55rem);letter-spacing:-.055em}.eyebrow{margin:0;color:var(--primary);font-size:.7rem;font-weight:800;letter-spacing:.15em;text-transform:uppercase}.subtitle,.auth-card p,.auth-card small{margin:.35rem 0 0;color:var(--muted);line-height:1.55}.auth-card,.create-card,.panel{border-radius:1.4rem;background:var(--surface);box-shadow:var(--raised)}.auth-card,.create-card{display:grid;gap:1.3rem;padding:1.25rem}.auth-card h2{margin:0;font-size:1.2rem}.auth-card form,.create-card form{display:grid;gap:.8rem;align-items:end}label{display:grid;gap:.45rem;color:var(--muted);font-size:.76rem;font-weight:800;text-transform:uppercase;letter-spacing:.07em}input{width:100%;padding:.8rem .9rem;border:0;border-radius:.82rem;color:var(--ink);background:var(--surface);box-shadow:var(--pressed);outline:none}input:focus{box-shadow:inset 4px 4px 9px var(--shadow-dark),inset -4px -4px 9px var(--shadow-light),0 0 0 3px rgba(92,110,232,.17)}button{padding:.8rem 1.1rem;border:0;border-radius:.82rem;color:#fff;background:linear-gradient(145deg,#7483ef,#4b5bd5);box-shadow:4px 4px 8px #c3cad4,-4px -4px 8px #fff;font-weight:800;cursor:pointer}button.secondary{color:var(--primary-dark);background:var(--surface);box-shadow:var(--raised-sm)}button:active{transform:translateY(1px);box-shadow:var(--pressed)}button:disabled{opacity:.55;cursor:not-allowed}.message{padding:.9rem 1rem;border-radius:.9rem;color:#187457;background:#dff5eb;box-shadow:var(--pressed)}.message.error{color:#b84d5b;background:#f8e4e7}.panel{padding:1rem}.table-header,.row{display:grid;grid-template-columns:.65fr .8fr .65fr 1fr;gap:.6rem;padding:.85rem .4rem}.table-header{display:none;color:var(--muted);text-transform:uppercase;font-size:.68rem;letter-spacing:.09em;font-weight:800}.row{border-top:1px solid #d8dee7;align-items:center}.row small{grid-column:1/-1;color:var(--muted)}.status{color:var(--success)}.empty{padding:1rem;color:var(--muted)}@media(min-width:620px){.auth-card,.create-card{grid-template-columns:1fr 1.5fr;align-items:center}.auth-card form,.create-card form{grid-template-columns:1fr 1fr auto auto}.table-header{display:grid}.table-header,.row{grid-template-columns:.6fr .8fr .7fr 1fr 1.6fr}.row small{grid-column:auto}}@media(max-width:480px){.heading{align-items:start;flex-direction:column}.heading .secondary{width:100%}}
  `]
})
export class OrdersPage implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly orders = signal<OrderSummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly signingIn = signal(false);
  protected readonly message = signal('');
  protected readonly hasError = signal(false);

  protected readonly loginForm = this.formBuilder.nonNullable.group({
    username: ['admin', Validators.required],
    password: ['', Validators.required]
  });

  protected readonly orderForm = this.formBuilder.nonNullable.group({
    productId: [101, [Validators.required, Validators.min(1)]],
    quantity: [1, [Validators.required, Validators.min(1)]]
  });

  ngOnInit(): void {
    this.loadOrders();
  }

  protected loadOrders(): void {
    this.loading.set(true);
    this.api.getOrders().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: orders => this.orders.set([...orders].sort((left, right) => right.id - left.id)),
      error: error => this.showError(error, 'Could not load orders.')
    });
  }

  protected login(): void {
    if (this.loginForm.invalid || this.signingIn()) return;
    this.signingIn.set(true);
    this.auth.login(this.loginForm.getRawValue()).pipe(finalize(() => this.signingIn.set(false))).subscribe({
      next: () => {
        this.hasError.set(false);
        this.message.set('Signed in as Admin. You can now create orders.');
      },
      error: error => this.showError(error, 'Sign in failed.')
    });
  }

  protected createOrder(): void {
    if (this.orderForm.invalid || this.submitting()) return;
    this.submitting.set(true);
    this.api.createOrder(this.orderForm.getRawValue()).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: order => {
        this.orders.update(orders => [order, ...orders.filter(existing => existing.id !== order.id)]);
        this.hasError.set(false);
        this.message.set(`Order #${order.id} was created successfully.`);
        this.orderForm.patchValue({ quantity: 1 });
      },
      error: error => {
        if (error.status === 401 || error.status === 403) this.auth.logout();
        this.showError(error, 'Order creation failed.');
      }
    });
  }

  protected logout(): void {
    this.auth.logout();
    this.message.set('Signed out.');
    this.hasError.set(false);
  }

  private showError(error: any, fallback: string): void {
    this.hasError.set(true);
    this.message.set(error?.error?.message ?? error?.error?.detail ?? error?.error?.title ?? fallback);
  }
}
