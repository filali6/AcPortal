import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({ providedIn: 'root' })
export class TooltipService {
  private tooltipEl: HTMLElement | null = null;
  private timer: any;
  private readonly DELAY = 300;
  private readonly MARGIN = 8; // espace minimum par rapport aux bords

  constructor(private translate: TranslateService) {}

  init() {
    document.addEventListener('mouseover', this.onShow);
    document.addEventListener('mouseout', this.onHide);
  }

  destroy() {
    document.removeEventListener('mouseover', this.onShow);
    document.removeEventListener('mouseout', this.onHide);
  }

  private onShow = (e: Event) => {
    const target = (e.target as HTMLElement)?.closest('[data-tooltip]') as HTMLElement;
    if (!target) return;
    const key = target.getAttribute('data-tooltip');
    if (!key) return;

    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.show(target, this.translate.instant(key));
    }, this.DELAY);
  };

  private onHide = () => {
    clearTimeout(this.timer);
    this.hide();
  };

  private show(target: HTMLElement, text: string) {
    this.hide();

    // Créer le tooltip
    this.tooltipEl = document.createElement('span');
    this.tooltipEl.textContent = text;
    this.tooltipEl.className = 'app-tooltip';
    // On le rend invisible d'abord pour mesurer sa taille
    this.tooltipEl.style.visibility = 'hidden';
    document.body.appendChild(this.tooltipEl);

    const rect = target.getBoundingClientRect();
    const tooltipRect = this.tooltipEl.getBoundingClientRect();
    const screenW = window.innerWidth;
    const screenH = window.innerHeight;

    // Position idéale : centré au-dessus de l'élément
    let top = rect.top - tooltipRect.height - this.MARGIN;
    let left = rect.left + rect.width / 2 - tooltipRect.width / 2;

    // Dépasse en HAUT → on met en dessous
    if (top < this.MARGIN) {
      top = rect.bottom + this.MARGIN;
    }

    // Dépasse en BAS → on remet au-dessus (cas rare)
    if (top + tooltipRect.height > screenH - this.MARGIN) {
      top = rect.top - tooltipRect.height - this.MARGIN;
    }

    // Dépasse à GAUCHE → coller au bord gauche
    if (left < this.MARGIN) {
      left = this.MARGIN;
    }

    // Dépasse à DROITE → coller au bord droit
    if (left + tooltipRect.width > screenW - this.MARGIN) {
      left = screenW - tooltipRect.width - this.MARGIN;
    }

    // Appliquer la position et rendre visible
    this.tooltipEl.style.top = `${top}px`;
    this.tooltipEl.style.left = `${left}px`;
    this.tooltipEl.style.visibility = 'visible';
  }

  private hide() {
    this.tooltipEl?.remove();
    this.tooltipEl = null;
  }
}