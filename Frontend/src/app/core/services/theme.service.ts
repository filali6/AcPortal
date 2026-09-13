import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  // signal pour que les composants puissent réagir au changement si besoin (ex: icône soleil/lune)
  readonly theme = signal<Theme>('light');

  constructor() {
    const saved = localStorage.getItem(STORAGE_KEY) as Theme | null;

    if (saved === 'light' || saved === 'dark') {
      this.setTheme(saved);
    } else {
      // pas de préférence enregistrée : on suit le thème du système
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      this.setTheme(prefersDark ? 'dark' : 'light');
    }
  }

  toggleTheme(): void {
    this.setTheme(this.theme() === 'light' ? 'dark' : 'light');
  }

  setTheme(theme: Theme): void {
    this.theme.set(theme);
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem(STORAGE_KEY, theme);
  }
}