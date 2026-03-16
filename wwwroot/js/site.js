// ================================================
// PizzaDeli – site.js
// Cart Drawer + UI interactions
// ================================================

// ---- Trạng thái giỏ hàng (lưu localStorage) ----
const CART_KEY = 'pizzadeli_cart';

function getCart() {
    try { 
        let cart = JSON.parse(localStorage.getItem(CART_KEY)) || []; 
        // Tự động dọn dẹp các sản phẩm lỗi (tên là undefined hoặc giá lỗi)
        const ogLen = cart.length;
        cart = cart.filter(i => i.id && i.name && i.name !== 'undefined');
        // Tuỳ chọn lưu lại luôn ổ cứng
        if (cart.length !== ogLen) {
            localStorage.setItem(CART_KEY, JSON.stringify(cart));
        }
        return cart;
    }
    catch { return []; }
}

function saveCart(cart) {
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
}

// Format tiền VND
function formatVND(amount) {
    if (amount == null || isNaN(amount)) amount = 0;
    return amount.toLocaleString('vi-VN') + ' ₫';
}

// ---- Render giỏ hàng trong Drawer ----
function renderCartDrawer() {
    const cart = getCart();
    const itemsContainer = document.getElementById('cartDrawerItems');
    const emptyState     = document.getElementById('cartEmptyState');
    const footer         = document.getElementById('cartDrawerFooter');
    
    // Header
    const countBadge     = document.querySelector('.cart-badge');
    const drawerCount    = document.getElementById('drawerItemCount');
    
    // Footer - New Structure
    const subtotalEl     = document.getElementById('drawerSubtotal');
    const taxEl          = document.getElementById('drawerTax');
    const totalPriceEl   = document.getElementById('drawerTotalPrice');

    if (!itemsContainer) return;

    // Xóa các item card cũ (giữ lại empty state)
    const oldCards = itemsContainer.querySelectorAll('.cart-item-card');
    oldCards.forEach(el => el.remove());

    const totalItems = cart.reduce((s, i) => s + (parseInt(i.quantity) || 0), 0);
    const subtotal   = cart.reduce((s, i) => s + (parseFloat(i.price) || 0) * (parseInt(i.quantity) || 0), 0);
    const tax        = subtotal * 0.1; // 10% delivery tax mock
    const grandTotal = subtotal + tax;

    // Cập nhật badge trên nút cart ở navbar
    if (countBadge) countBadge.textContent = totalItems;
    if (drawerCount) drawerCount.textContent = totalItems;

    if (cart.length === 0) {
        if (emptyState) emptyState.style.display = '';
        if (footer)     footer.style.display     = 'none';
        return;
    }

    if (emptyState) emptyState.style.display = 'none';
    if (footer)     footer.style.display     = '';

    if (subtotalEl)   subtotalEl.textContent   = formatVND(subtotal);
    if (taxEl)        taxEl.textContent        = formatVND(tax);
    if (totalPriceEl) totalPriceEl.textContent = formatVND(grandTotal);

    cart.forEach(function (item) {
        const card = document.createElement('div');
        card.className = 'cart-item-card';
        card.dataset.productId = item.id;

        card.innerHTML = `
            <img class="cart-item-img"
                 src="${item.image || '/images/placeholder.png'}"
                 alt="${item.name}"
                 onerror="this.src='/images/placeholder.png'">
            <div class="cart-item-info">
                <div class="cart-item-name" title="${item.name}">${item.name}</div>
                <div class="cart-item-price">${formatVND(item.price)}</div>
                <div class="cart-item-qty-row">
                    <button class="qty-btn btn-qty-decrease" data-id="${item.id}" title="Giảm">−</button>
                    <span class="qty-value">${item.quantity}</span>
                    <button class="qty-btn btn-qty-increase" data-id="${item.id}" title="Tăng">+</button>
                </div>
            </div>
            <button class="cart-item-remove" data-id="${item.id}" title="Xóa khỏi giỏ">
                <span class="material-symbols-outlined">delete</span>
            </button>
        `;
        itemsContainer.appendChild(card);
    });

    // Gắn sự kiện tăng/giảm/xóa
    itemsContainer.querySelectorAll('.btn-qty-decrease').forEach(function (btn) {
        btn.addEventListener('click', function () {
            changeQty(btn.dataset.id, -1);
        });
    });
    itemsContainer.querySelectorAll('.btn-qty-increase').forEach(function (btn) {
        btn.addEventListener('click', function () {
            changeQty(btn.dataset.id, 1);
        });
    });
    itemsContainer.querySelectorAll('.cart-item-remove').forEach(function (btn) {
        btn.addEventListener('click', function () {
            removeFromCart(btn.dataset.id);
        });
    });
}

// ---- Thao tác giỏ hàng ----
function addToCart(product) {
    // product = { id, name, price, image }
    const cart = getCart();
    const idx  = cart.findIndex(function (i) { return i.id === product.id; });
    if (idx >= 0) {
        cart[idx].quantity += product.quantity || 1;
    } else {
        cart.push({ ...product, quantity: product.quantity || 1 });
    }
    saveCart(cart);
    renderCartDrawer();
    openCartDrawer();
}

function changeQty(productId, delta) {
    const cart = getCart();
    const idx  = cart.findIndex(function (i) { return i.id === productId; });
    if (idx < 0) return;
    cart[idx].quantity += delta;
    if (cart[idx].quantity <= 0) cart.splice(idx, 1);
    saveCart(cart);
    renderCartDrawer();
}

function removeFromCart(productId) {
    let cart = getCart();
    cart = cart.filter(function (i) { return i.id !== productId; });
    saveCart(cart);
    renderCartDrawer();
}

// ---- Mở / Đóng Drawer ----
function openCartDrawer() {
    const drawer  = document.getElementById('cartDrawer');
    const overlay = document.getElementById('cartOverlay');
    if (drawer)  drawer.classList.add('open');
    if (overlay) overlay.classList.add('open');
    document.body.style.overflow = 'hidden';
    renderCartDrawer();
}

function closeCartDrawer() {
    const drawer  = document.getElementById('cartDrawer');
    const overlay = document.getElementById('cartOverlay');
    if (drawer)  drawer.classList.remove('open');
    if (overlay) overlay.classList.remove('open');
    document.body.style.overflow = '';
}

// ---- Gắn sự kiện khi DOM sẵn sàng ----
document.addEventListener('DOMContentLoaded', function () {

    // Render badge ban đầu
    renderCartDrawer();

    // Nút mở giỏ hàng (class btn-cart hoặc id cartBtn)
    const cartBtn = document.querySelector('.btn-cart');
    if (cartBtn) {
        cartBtn.addEventListener('click', function (e) {
            e.preventDefault();
            openCartDrawer();
        });
    }

    // Đóng: nút X
    const closeBtn = document.getElementById('cartDrawerClose');
    if (closeBtn) closeBtn.addEventListener('click', closeCartDrawer);

    // Đóng: click overlay
    const overlay = document.getElementById('cartOverlay');
    if (overlay) overlay.addEventListener('click', closeCartDrawer);

    // Đóng: phím Escape
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeCartDrawer();
    });

    // --- Filter buttons (category) ---
    document.querySelectorAll('.btn-filter').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const group = btn.closest('.category-filters');
            if (group) {
                group.querySelectorAll('.btn-filter').forEach(function (b) {
                    b.classList.remove('active');
                });
            }
            btn.classList.add('active');
        });
    });

    // --- Mobile search toggle ---
    const mobileSearchBtn = document.getElementById('mobile-search-btn');
    const searchWrapper   = document.querySelector('.search-wrapper');
    if (mobileSearchBtn && searchWrapper) {
        mobileSearchBtn.addEventListener('click', function () {
            const isOpen = searchWrapper.style.display === 'block';
            searchWrapper.style.display = isOpen ? '' : 'block';
            if (!isOpen) searchWrapper.querySelector('.search-input')?.focus();
        });
    }

});

// ==========================================
// Handlers xử lý Nút Gọi Trực Tiếp từ HTML
// ==========================================
function getProductDataFromBtn(btn) {
    return {
        id:    btn.dataset.productId   || 'product-' + Date.now(),
        name:  btn.dataset.productName || 'Sản phẩm',
        price: parseFloat(btn.dataset.productPrice || 0),
        image: btn.dataset.productImage || '',
        cat:   btn.dataset.productCat || '',
        desc:  btn.dataset.productDesc || ''
    };
}

let currentPizzaToCart = null;
let currentPizzaModalQty = 1;

window.onAddToCartClick = function (btn) {
    const product = getProductDataFromBtn(btn);
    // Nếu là Pizza thì bung Modal
    const isPizza = (product.cat || '').toLowerCase().includes('pizza') || (product.name || '').toLowerCase().includes('pizza');
    if (isPizza) {
        openPizzaDetailModal(product);
        return;
    }
    
    addToCart(product);

    // Feedback visual
    const originalHTML = btn.innerHTML;
    btn.innerHTML = '<span class="material-symbols-outlined">check</span>';
    btn.style.backgroundColor = 'var(--primary)';
    btn.style.color = '#fff';
    setTimeout(function () {
        btn.innerHTML = originalHTML;
        btn.style.backgroundColor = '';
        btn.style.color = '';
    }, 1200);
};

window.onBuyNowClick = function (btn) {
    const product = getProductDataFromBtn(btn);
    
    // Nếu là Pizza bung Modal (khi confirm sẽ Add to cart sau đó mình redirect thủ công)
    const isPizza = (product.cat || '').toLowerCase().includes('pizza') || (product.name || '').toLowerCase().includes('pizza');
    if (isPizza) {
        openPizzaDetailModal(product, true);
        return;
    }

    // Thêm trực tiếp nếu ko phải Pizza
    const cart = getCart();
    const idx  = cart.findIndex(i => i.id === product.id);
    if (idx >= 0) {
        cart[idx].quantity += 1;
    } else {
        cart.push({ ...product, quantity: 1 });
    }
    saveCart(cart);
    window.location.href = '/Customer/Checkout';
};

// ==========================================
// THÊM CHỨC NĂNG MODAL CHO PIZZA
// ==========================================
let isBuyNowMode = false;
function openPizzaDetailModal(product, buyNow = false) {
    isBuyNowMode = buyNow;
    currentPizzaToCart = product;
    currentPizzaModalQty = 1;

    document.getElementById('pzModalImg').src = product.image || '/images/placeholder.png';
    document.getElementById('pzModalName').textContent = product.name;
    document.getElementById('pzModalDesc').textContent = product.desc || 'Hương vị tuyệt hảo trên từng lớp phô mai nóng hổi được nướng hoàn hảo.';
    document.getElementById('pzModalQty').textContent = 1;
    document.getElementById('pzModalPriceTotal').textContent = formatVND(product.price);

    // Reset styles of Options
    document.querySelectorAll('.pz-radio-btn').forEach(l => l.classList.remove('active'));
    document.querySelectorAll('.pz-radio-btn input').forEach(inp => inp.checked = false);
    document.querySelectorAll('.pz-check-btn').forEach(l => l.classList.remove('active'));
    document.querySelectorAll('.pz-check-btn input').forEach(inp => inp.checked = false);
    
    // Select default Crust & Cheese
    const crustFirst = document.querySelector('input[name="pz-crust"]');
    if (crustFirst) { crustFirst.checked = true; crustFirst.parentElement.classList.add('active'); }
    
    const cheeseFirst = document.querySelector('input[name="pz-cheese"]');
    if (cheeseFirst) { cheeseFirst.checked = true; cheeseFirst.parentElement.classList.add('active'); }

    // Toggle overlay
    const modal = document.getElementById('pizzaDetailModal');
    if (modal) {
        modal.style.display = 'flex';
        // forced reflow for animation
        void modal.offsetWidth; 
        modal.classList.add('show');
    }
}

function closePizzaModal() {
    const modal = document.getElementById('pizzaDetailModal');
    if (modal) {
        modal.classList.remove('show');
        setTimeout(() => { modal.style.display = 'none'; }, 300);
    }
}

function changePzModalQty(delta) {
    currentPizzaModalQty += delta;
    if(currentPizzaModalQty < 1) currentPizzaModalQty = 1;
    document.getElementById('pzModalQty').textContent = currentPizzaModalQty;
    if(currentPizzaToCart) {
        const total = parseFloat(currentPizzaToCart.price || 0) * currentPizzaModalQty;
        document.getElementById('pzModalPriceTotal').textContent = formatVND(total);
    }
}

function updatePzRadio(radioInput) {
    const name = radioInput.name;
    document.querySelectorAll(`input[name="${name}"]`).forEach(input => {
        input.parentElement.classList.remove('active');
    });
    radioInput.parentElement.classList.add('active');
}

function updatePzCheck(checkInput) {
    if(checkInput.checked) checkInput.parentElement.classList.add('active');
    else checkInput.parentElement.classList.remove('active');
}

function confirmPzModalAdd() {
    if(!currentPizzaToCart) return;

    // Get value
    const crust = document.querySelector('input[name="pz-crust"]:checked')?.value || 'Thin';
    const cheese = document.querySelector('input[name="pz-cheese"]:checked')?.value || 'Mozzarella';
    const checkedToppings = document.querySelectorAll('input[name="pz-topping"]:checked');
    const toppingList = Array.from(checkedToppings).map(el => el.value).join(', ');

    let addedName = currentPizzaToCart.name;
    addedName += ` (Đế: ${crust}, Trí: ${cheese})`;
    if (toppingList) addedName += ` + ${toppingList}`;

    const customizedProduct = {
        id: currentPizzaToCart.id + '-' + crust + '-' + cheese + (toppingList ? '-' + toppingList.replace(/, /g,'').substring(0,5) : ''),
        name: addedName,
        price: currentPizzaToCart.price,
        image: currentPizzaToCart.image
    };

    // Luôn add đủ quantity vào thẻ qua localstorage
    const cart = getCart();
    const idx = cart.findIndex(i => i.id === customizedProduct.id);
    if(idx >= 0) cart[idx].quantity += currentPizzaModalQty;
    else {
        customizedProduct.quantity = currentPizzaModalQty;
        cart.push(customizedProduct);
    }
    saveCart(cart);

    closePizzaModal();
    
    if (isBuyNowMode) {
        window.location.href = '/Customer/Checkout';
    } else {
        renderCartDrawer();
        openCartDrawer();
    }
}

function formatVND(amount) {
    if (typeof amount !== 'number') return '0 ₫';
    return amount.toLocaleString('vi-VN') + ' ₫';
}

// Expose addToCart toàn cục cho các trang khác có thể gọi
window.PizzaCart = { addToCart, openCartDrawer, closeCartDrawer, getCart };
