export interface OrderSummary {
  id: number;
  productId: number;
  quantity: number;
  status: string;
  createdAtUtc: string;
}

export interface CreateOrderRequest {
  productId: number;
  quantity: number;
}
