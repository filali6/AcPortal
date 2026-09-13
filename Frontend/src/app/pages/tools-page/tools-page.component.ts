import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PluginBridgeService, Plugin } from '../../core/services/plugin-bridge.service';
import { TranslateModule } from '@ngx-translate/core';
import { DrawerService } from '../../core/services/drawer.service';
import { LucideAngularModule, Plus, Trash2, Lock, Check, X, Search, Store } from 'lucide-angular';

type StatusFilter = 'all' | 'active' | 'inactive';

// Mapping local : id du plugin → fichier local dans src/assets/logos/
// Utilisé uniquement si le backend ne fournit pas encore `icon`.
// Dépose les fichiers correspondants dans src/assets/logos/ et complète cette liste.
const LOCAL_LOGO_MAP: Record<string, string> = {
  'axe-brm': 'assets/logos/axe-brm.png',
  'axe-acp': 'assets/logos/axe-acp.png',
  'gui-sso': 'assets/logos/gui-sso.png',
  'axe-gui': 'assets/logos/axe-gui.png',
  'axe-glue': 'assets/logos/axe-glue.png',
};

@Component({
  selector: 'app-tools-page',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, LucideAngularModule],
  templateUrl: './tools-page.component.html',
  styleUrl: './tools-page.component.scss'
})
export class ToolsPageComponent implements OnInit {
  myPlugins: Plugin[] = [];
  allPlugins: Plugin[] = [];
  loading = true;
  marketplaceOpen = false;

  // Filtres "Mes outils"
  activeFilter: StatusFilter = 'all';

  // Recherche + catégorie dans le drawer marketplace
  searchTerm = '';
  selectedCategory = 'all';
  addedSectionCollapsed = true;

  readonly Plus = Plus;
  readonly Trash2 = Trash2;
  readonly Lock = Lock;
  readonly Check = Check;
  readonly X = X;
  readonly Search = Search;
  readonly Store = Store;

  constructor(
    private pluginBridge: PluginBridgeService,
    private router: Router,
    private drawerService: DrawerService
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.pluginBridge.getAllPlugins().subscribe({
      next: (all) => {
        this.allPlugins = all;
        this.pluginBridge.getMyPlugins().subscribe({
          next: (my) => {
            // getMyPlugins() ne renvoie pas isActive/icon à jour :
            // on enrichit chaque plugin ajouté avec les données fraîches de allPlugins (source de vérité)
            this.myPlugins = my.map(p => {
              const source = all.find(a => a.id === p.id);
              return source
                ? { ...p, isActive: source.isActive ?? true, icon: source.icon, category: source.category }
                : { ...p, isActive: p.isActive ?? true };
            });
            this.loading = false;
          }
        });
      }
    });
  }

  // ── Mes outils ──

  get filteredMyPlugins(): Plugin[] {
    if (this.activeFilter === 'all') return this.myPlugins;
    if (this.activeFilter === 'active') return this.myPlugins.filter(p => p.isActive);
    return this.myPlugins.filter(p => !p.isActive);
  }

  setFilter(filter: StatusFilter): void {
    this.activeFilter = filter;
  }

  // Un outil ajouté par l'utilisateur mais désactivé entre-temps par le superadmin
  isSuspended(plugin: Plugin): boolean {
    return !plugin.isActive;
  }

  // ── Marketplace (drawer) ──

  get categories(): string[] {
    const cats = new Set(this.allPlugins.map(p => p.category));
    return Array.from(cats);
  }

  // Tools non ajoutés, actifs uniquement, filtrés par recherche/catégorie
  get availablePlugins(): Plugin[] {
    return this.allPlugins.filter(p => {
      if (!p.isActive) return false;
      if (this.isAdded(p.id)) return false;
      if (this.selectedCategory !== 'all' && p.category !== this.selectedCategory) return false;
      if (this.searchTerm.trim()) {
        const term = this.searchTerm.trim().toLowerCase();
        return p.name.toLowerCase().includes(term) || p.description.toLowerCase().includes(term);
      }
      return true;
    });
  }

  // Tools déjà ajoutés (même s'ils sont devenus inactifs entre-temps)
  get addedPlugins(): Plugin[] {
    return this.allPlugins.filter(p => this.isAdded(p.id));
  }

  toggleAddedSection(): void {
    this.addedSectionCollapsed = !this.addedSectionCollapsed;
  }

  isAdded(pluginId: string): boolean {
    return this.myPlugins.some(p => p.id === pluginId);
  }

  addPlugin(pluginId: string): void {
    this.pluginBridge.addPlugin(pluginId).subscribe({
      next: () => this.loadData()
    });
  }

  removePlugin(pluginId: string): void {
    this.pluginBridge.removePlugin(pluginId).subscribe({
      next: () => this.loadData()
    });
  }

  openPlugin(plugin: Plugin): void {
    if (this.isSuspended(plugin)) return; // sécurité, le bouton est déjà désactivé côté template
    window.open(plugin.accessUrl, '_blank');
  }

  openMarketplace(): void {
    this.marketplaceOpen = true;
    this.drawerService.open();
  }

  closeMarketplace(): void {
    this.marketplaceOpen = false;
    this.drawerService.close();
  }

  // ── Icônes ──

  // Priorité : icon fourni par le backend, sinon mapping local, sinon rien (fallback initiale CSS)
  getLogo(plugin: Plugin): string | null {
    if (plugin.icon && plugin.icon.trim()) return plugin.icon;
    return LOCAL_LOGO_MAP[plugin.id] ?? null;
  }

  onIconError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }
}