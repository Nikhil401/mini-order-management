import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private static readonly themeKey = 'mini-orders-theme';

  protected readonly isDarkTheme = signal(localStorage.getItem(App.themeKey) === 'dark');

  protected toggleTheme(): void {
    this.isDarkTheme.update(isDark => !isDark);
    localStorage.setItem(App.themeKey, this.isDarkTheme() ? 'dark' : 'light');
  }
}
